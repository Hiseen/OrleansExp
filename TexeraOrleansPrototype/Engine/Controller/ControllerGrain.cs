using Orleans;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using Engine.OperatorImplementation.Operators;
using Engine.OperatorImplementation.Common;
using Orleans.Runtime;
using TexeraUtilities;
using Newtonsoft.Json.Linq;
using System.Linq;
using Engine.DeploySemantics;
using Engine.LinkSemantics;
using Engine.Breakpoint.GlobalBreakpoint;
using Engine.OperatorImplementation.FaultTolerance;
using Orleans.Concurrency;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Controller
{
    [WorkerGrainPlacement]
    public class ControllerGrain : Grain, IControllerGrain
    {
        private IControllerGrain self;
        private int currentRepliedPrincipals=0;
        private int targetRepliedPrincipals=0;
        private int numberOfOutputGrains = 0;
        private DateTime actionStart;
        private bool performingAction=false;
        private Dictionary<Guid,IPrincipalGrain> nodes = new Dictionary<Guid, IPrincipalGrain>();
        private Dictionary<IPrincipalGrain,bool> isPrincipalPaused = new Dictionary<IPrincipalGrain, bool>();
        private Dictionary<Guid,Operator> nodeMetadata = new Dictionary<Guid, Operator>();
        private List<LinkStrategy> nodeLinks = new List<LinkStrategy>();
        private Dictionary<Guid,List<Guid>> forwardLinks = new Dictionary<Guid, List<Guid>>();
        private Dictionary<Guid,List<Guid>> backwardLinks = new Dictionary<Guid, List<Guid>>();
        private Dictionary<Guid, HashSet<Guid>> startDependencies = new Dictionary<Guid, HashSet<Guid>>();
        private ILocalSiloDetails localSiloDetails => this.ServiceProvider.GetRequiredService<ILocalSiloDetails>();

        public async Task<SiloAddress> Init(IControllerGrain self,string plan,bool checkpointActivated = false)
        {
            this.self = self;
            ApplyLogicalPlan(CompileLogicalPlan(plan,checkpointActivated));
            await InitOperators();
            var sinks = nodes.Keys.Where(x => nodeMetadata[x].GetType()!= typeof(HashBasedMaterializerOperator) && !forwardLinks.ContainsKey(x)).ToList();
            await LinkToObserver(sinks);
            return localSiloDetails.SiloAddress;
        }

        private async Task LinkToObserver(List<Guid> ids)
        {
            var streamProvider = GetStreamProvider("SMSProvider");
            var stream = streamProvider.GetStream<Immutable<PayloadMessage>>(this.GetPrimaryKey(),"OutputStream");
            foreach(var id in ids)
            {
                var layer = await nodes[id].GetOutputLayer();
                numberOfOutputGrains+=layer.Layer.Values.SelectMany(x=>x).Count();
                var link = new ObserverLinking(stream,layer);
                await link.Link();
                nodeLinks.Add(link);
            }

        }


        public override Task OnDeactivateAsync()
        {
            Console.WriteLine("Controller: "+ OperatorImplementation.Common.Utils.GetReadableName(self)+" deactivated");
            nodes = null;
            nodeLinks = null;
            nodeMetadata = null;
            forwardLinks = null;
            backwardLinks = null;
            startDependencies = null;
            return base.OnDeactivateAsync();
        }

        private void AddStartDenpendency(Guid target, Guid dependsOn)
        {
            if(startDependencies.ContainsKey(target))
            {
                startDependencies[target].Add(dependsOn);
            }
            else
            {
                startDependencies[target]= new HashSet<Guid>{dependsOn};
            }
        }


        private async Task InitOperators()
        {
            //topological ordering
            var current = nodeMetadata.Keys.Where(x => !backwardLinks.ContainsKey(x)).ToList();
            while(current.Count > 0)
            {
                foreach(var id in current)
                {
                    IPrincipalGrain principal = GrainFactory.GetGrain<IPrincipalGrain>(id);
                    nodes.Add(id,principal);
                    RequestContext.Clear();
                    RequestContext.Set("targetSilo",Constants.ClientIPAddress);
                    if(backwardLinks.ContainsKey(id))
                    {
                        List<Pair<Operator,WorkerLayer>> prev = new List<Pair<Operator, WorkerLayer>>();
                        List<Guid> prevIDs = new List<Guid>();
                        foreach(var prevID in backwardLinks[id])
                        {
                            var t = await nodes[prevID].GetOutputLayer();
                            prevIDs.Add(prevID);
                            prev.Add(new Pair<Operator,WorkerLayer>(nodeMetadata[prevID],t));
                        }
                        await principal.Init(self,nodeMetadata[id],prev);
                        var inputLayer = await principal.GetInputLayer();
                        for(int i=0;i<prev.Count;++i)
                        {
                            var pair = prev[i];
                            var prevID = prevIDs[i];
                            if(nodeMetadata[prevID].GetType()==typeof(HashBasedFolderScanOperator))
                            {
                                var link = new OneToOneLinking(pair.Second,inputLayer,Constants.BatchSize);
                                await link.Link();
                                nodeLinks.Add(link);
                            }
                            else if(nodeMetadata[id].IsStaged(pair.First))
                            {
                                var link = new RoundRobinLinking(pair.Second,inputLayer,Constants.BatchSize);
                                await link.Link();
                                nodeLinks.Add(link);
                            }
                            else
                            {
                                var link = new HashBasedShuffleLinking(nodeMetadata[id].GetHashFunctionAsString(prevID),pair.Second,inputLayer,Constants.BatchSize);
                                await link.Link();
                                nodeLinks.Add(link);   
                            }
                        }
                    }
                    else
                    {
                        await principal.Init(self,nodeMetadata[id],null);
                    }
                }
                current = current.SelectMany(x => 
                {
                    if(forwardLinks.ContainsKey(x))
                        return forwardLinks[x];
                    else
                        return Enumerable.Empty<Guid>();
                }).Where(x => backwardLinks[x].All(y => nodes.ContainsKey(y))).Distinct().ToList();
            }
        }


        private JObject CompileLogicalPlan(string plan, bool checkpointActivated)
        {
            JObject res = JObject.Parse(plan);
            if(checkpointActivated)
            {
                JArray operators = (JArray)res["operators"];
                JArray links =(JArray)res["links"];
                JArray operatorsToAdd = new JArray();
                JArray linksToAdd = new JArray(); 
                foreach(JObject op in operators)
                {
                    string operatorType = (string)op["operatorType"];
                    string currentID = (string)op["operatorID"];
                    if(operatorType == "HashRippleJoin" || operatorType == "HashJoin")
                    {
                        var linksToDelete = new List<JToken>();
                        //search for input links
                        for(int i=0;i<links.Count;++i)
                        {
                            var link = links[i];
                            if(((string)link["destination"]).Equals(currentID))
                            {
                                //create new links
                                string inputID = (string)link["origin"];
                                Guid materializerID = Guid.NewGuid(); 
                                Guid scanID = Guid.NewGuid();
                                //set start dependency
                                AddStartDenpendency(scanID,materializerID);
                                var link1 = new JObject
                                {
                                    ["origin"]=inputID,
                                    ["destination"]="operator-"+materializerID.ToString()
                                };
                                var link2 = new JObject
                                {
                                    ["origin"]="operator-"+scanID.ToString(),
                                    ["destination"]=currentID
                                };
                                linksToAdd.Add(link1);
                                linksToAdd.Add(link2);
                                //create materializer operator
                                JObject materializer = new JObject
                                {
                                    ["operatorType"]="HashBasedMaterializer",
                                    ["operatorID"]="operator-"+materializerID.ToString(),
                                    ["hasherID"]=currentID,
                                    ["inputID"]="operator-"+scanID.ToString(),
                                };
                                operatorsToAdd.Add(materializer);
                                //create scan operator
                                JObject scan = new JObject
                                {
                                    ["operatorType"]="HashBasedFolderScan",
                                    ["operatorID"]="operator-"+scanID.ToString(),
                                    ["folderRoot"]=materializerID.ToString()
                                };
                                operatorsToAdd.Add(scan);
                                linksToDelete.Add(link);
                            }
                        }
                        foreach(var link in linksToDelete)
                        {
                            link.Remove();
                        }
                    }
                }
                links.Merge(linksToAdd);
                operators.Merge(operatorsToAdd);
            }
            return res;
        }


        private void ApplyLogicalPlan(JObject plan)
        {
            Console.WriteLine(plan);
            JArray operators = (JArray)plan["operators"];
            int batchSize = Constants.BatchSize;
            JArray jsonLinks = (JArray)plan["links"];
            foreach(JObject link in jsonLinks)
            {
                Guid origin = Guid.Parse(link["origin"].ToString().Substring(9));
                Guid dest = Guid.Parse(link["destination"].ToString().Substring(9));
                if(forwardLinks.ContainsKey(origin))
                {
                    forwardLinks[origin].Add(dest);
                }
                else
                {
                    forwardLinks[origin]=new List<Guid>{dest};
                }
                if(backwardLinks.ContainsKey(dest))
                {
                    backwardLinks[dest].Add(origin);
                }
                else
                {
                    backwardLinks[dest]=new List<Guid>{origin};
                }
            }

            foreach (JObject operator1 in operators)
            {
                Operator op=null;
                Guid operatorID=Guid.Parse(operator1["operatorID"].ToString().Substring(9));
                if((string)operator1["operatorType"] == "ScanSource")
                {
                    //example path to HDFS through WebHDFS API: "http://localhost:50070/webhdfs/v1/input/very_large_input.csv"
                    op = new ScanOperator((string)operator1["tableName"]);
                }
                else if((string)operator1["operatorType"] == "KeywordMatcher")
                {
                    op = new KeywordOperator(int.Parse(operator1["attributeName"].ToString().Replace("_c","")),operator1["keyword"]!=null?operator1["keyword"].ToString():"");
                }
                else if((string)operator1["operatorType"] == "Aggregation")
                {
                    op = new CountOperator();
                }
                else if((string)operator1["operatorType"] == "Comparison")
                {
                    switch(operator1["attributeType"].ToString())
                    {
                        case "int":
                            op = new FilterOperator<int>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")),operator1["compareTo"].ToString(),operator1["comparisonType"].ToString());
                            break;
                        case "double":
                            op = new FilterOperator<double>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")),operator1["compareTo"].ToString(),operator1["comparisonType"].ToString());
                            break;
                        case "date":
                            op=new FilterOperator<DateTime>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")),operator1["compareTo"].ToString(),operator1["comparisonType"].ToString());
                            break;
                        case "string":
                            op=new FilterOperator<string>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")),operator1["compareTo"].ToString(),operator1["comparisonType"].ToString());
                            break;
                    }
                }
                else if((string)operator1["operatorType"] == "CrossRippleJoin")
                {
                    int innerIndex=int.Parse(operator1["innerTableAttribute"].ToString().Replace("_c",""));
                    int outerIndex=int.Parse(operator1["outerTableAttribute"].ToString().Replace("_c",""));
                    AddStartDenpendency(backwardLinks[operatorID][1],backwardLinks[operatorID][0]);
                    op = new CrossRippleJoinOperator(innerIndex,outerIndex,backwardLinks[operatorID][0]);
                }
                else if((string)operator1["operatorType"] == "HashRippleJoin")
                {
                    int innerIndex=int.Parse(operator1["innerTableAttribute"].ToString().Replace("_c",""));
                    int outerIndex=int.Parse(operator1["outerTableAttribute"].ToString().Replace("_c",""));
                    AddStartDenpendency(backwardLinks[operatorID][1],backwardLinks[operatorID][0]);
                    op = new HashRippleJoinOperator(innerIndex,outerIndex,backwardLinks[operatorID][0]);
                }
                else if((string)operator1["operatorType"] == "InsertionSort")
                {
                    switch(operator1["attributeType"].ToString())
                    {
                        case "int":
                            op = new SortOperator<int>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")));
                            break;
                        case "double":
                            op = new SortOperator<double>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")));
                            break;
                        case "date":
                            op= new SortOperator<DateTime>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")));
                            break;
                        case "string":
                            op= new SortOperator<string>(int.Parse(operator1["attributeName"].ToString().Replace("_c","")));
                            break;
                    }
                }
                else if((string)operator1["operatorType"] == "GroupBy")
                {
                    int groupByIndex=int.Parse(operator1["groupByAttribute"].ToString().Replace("_c",""));
                    int aggregationIndex=int.Parse(operator1["aggregationAttribute"].ToString().Replace("_c",""));
                    op=new GroupByOperator(groupByIndex,aggregationIndex,operator1["aggregationFunction"].ToString());
                }
                else if((string)operator1["operatorType"] == "Projection")
                {
                    List<int> projectionIndexs=operator1["projectionAttributes"].ToString().Split(",").Select(x=>int.Parse(x.Replace("_c",""))).ToList();
                    op=new ProjectionOperator(projectionIndexs);
                }
                else if((string)operator1["operatorType"] == "HashJoin")
                {
                    int innerIndex=int.Parse(operator1["innerTableAttribute"].ToString().Replace("_c",""));
                    int outerIndex=int.Parse(operator1["outerTableAttribute"].ToString().Replace("_c",""));
                    AddStartDenpendency(backwardLinks[operatorID][1],backwardLinks[operatorID][0]);
                    op = new HashJoinOperator(innerIndex,outerIndex,backwardLinks[operatorID][0]);
                }
                else if((string)operator1["operatorType"]=="SentimentAnalysis")
                {
                    int predictIndex=int.Parse(operator1["targetAttribute"].ToString().Replace("_c",""));
                    op=new SentimentAnalysisOperator(predictIndex);
                }
                else if((string)operator1["operatorType"]=="HashBasedMaterializer")
                {
                    Guid hasherID = Guid.Parse(operator1["hasherID"].ToString().Substring(9));
                    Guid inputID = Guid.Parse(operator1["inputID"].ToString().Substring(9));
                    string hashFunc = nodeMetadata[hasherID].GetHashFunctionAsString(inputID);
                    op = new HashBasedMaterializerOperator(operatorID,Constants.DefaultNumGrainsInOneLayer,hashFunc);
                }
                else if((string)operator1["operatorType"]=="HashBasedFolderScan")
                {
                    op = new HashBasedFolderScanOperator(Constants.WebHDFSEntry+"/tmp/"+operator1["folderRoot"],Constants.DefaultNumGrainsInOneLayer);
                }

                if(op!=null)
                {
                    nodeMetadata.Add(operatorID,op);
                }
                else
                {
                    Console.WriteLine("operator implementation for "+(string)operator1["operatorType"]+" not found");
                }
            }
        }


        public async Task Pause()
        {
            if(performingAction)
            {
                Console.WriteLine("one action is performing, please wait...");
                return;
            }
            performingAction=true;
            currentRepliedPrincipals=0;
            actionStart=DateTime.UtcNow;
            foreach(IPrincipalGrain o in nodes.Values)
            {
                isPrincipalPaused[o]=false;
                await o.Pause();
            }
            return;
        }

        public Task OnTaskDidPaused()
        {   
            currentRepliedPrincipals++;
            //Console.WriteLine(currentPausedPrincipals+"  "+targetPausedPrincipals);
            if(currentRepliedPrincipals==targetRepliedPrincipals)
            {
                TimeSpan duration=DateTime.UtcNow-actionStart;
                Console.WriteLine("Workflow Paused in "+duration);
                performingAction=false;
            }
            return Task.CompletedTask;
        }


        public async Task Resume()
        {
            if(performingAction)
            {
                Console.WriteLine("one action is performing, please wait...");
                return;
            }
            performingAction=true;
            currentRepliedPrincipals=0;
            actionStart=DateTime.UtcNow;
            foreach(IPrincipalGrain o in nodes.Values)
            {
                await o.Resume();
            }
            TimeSpan duration=DateTime.UtcNow-actionStart;
            Console.WriteLine("Workflow Resumed in "+duration);
            performingAction=false;
        }

        public Task Start()
        {
            var sources = nodes.Keys.Where(x => !backwardLinks.ContainsKey(x) && !startDependencies.ContainsKey(x)).ToList();
            foreach(var id in sources)
            {
                Console.WriteLine("Controller: Starting "+ OperatorImplementation.Common.Utils.GetReadableName(nodes[id]));
                nodes[id].Start();
            }
            return Task.CompletedTask;
        }

        public async Task Deactivate()
        {
            foreach(var principal in nodes.Values)
            {
                await principal.Deactivate();
            }
            DeactivateOnIdle();
        }

        public Task OnPrincipalRunning(IPrincipalGrain sender)
        {
            return Task.CompletedTask;
        }

        public Task OnPrincipalPaused(IPrincipalGrain sender)
        {
            isPrincipalPaused[sender]=true;
            if(isPrincipalPaused.Values.All(x=> x == true))
            {
                TimeSpan duration=DateTime.UtcNow-actionStart;
                Console.WriteLine("Workflow Paused in "+duration);
                performingAction=false;
            }
            return Task.CompletedTask;
        }

        public Task OnPrincipalCompleted(IPrincipalGrain sender)
        {
            Console.WriteLine("Controller: "+sender.GetPrimaryKey()+" completed!");
            Guid id = sender.GetPrimaryKey();
            var itemToDelete = new List<Guid>();
            foreach(var pair in startDependencies)
            {
                if(pair.Value.Contains(id))
                {
                    pair.Value.Remove(id);
                }
                if(pair.Value.Count == 0)
                {
                    itemToDelete.Add(pair.Key);
                    nodes[pair.Key].Start();
                }
            }
            foreach(var item in itemToDelete)
            {
                startDependencies.Remove(item);
            }
            return Task.CompletedTask;
        }

        public async Task SetBreakpoint(Guid operatorID, GlobalBreakpointBase breakpoint)
        {
            await nodes[operatorID].SetBreakpoint(breakpoint);
        }

        public Task OnBreakpointTriggered(string report)
        {
            Console.WriteLine(report);
            return Task.CompletedTask;
        }

        public Task<int> GetNumberOfOutputGrains()
        {
            return Task.FromResult(numberOfOutputGrains);
        }
    }
}