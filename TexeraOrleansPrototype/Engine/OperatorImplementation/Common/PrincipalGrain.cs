using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Engine.OperatorImplementation.Operators;
using Engine.OperatorImplementation.SendingSemantics;
using TexeraUtilities;
using System.Collections.ObjectModel;
using Engine.Controller;
using System.Linq;

namespace Engine.OperatorImplementation.Common
{
    public class PrincipalGrain : Grain, IPrincipalGrain
    {
        public virtual int DefaultNumGrainsInOneLayer { get { return 6; } }
        private List<IPrincipalGrain> nextPrincipalGrains = new List<IPrincipalGrain>();
        private List<IPrincipalGrain> prevPrincipalGrains = new List<IPrincipalGrain>();
        protected bool isPaused = false;
        protected Guid operatorID;
        protected List<List<IWorkerGrain>> operatorGrains = new List<List<IWorkerGrain>>();
        protected List<IWorkerGrain> outputGrains {get{return operatorGrains.Last();}}
        protected List<IWorkerGrain> inputGrains {get{return operatorGrains.First();}}
        protected PredicateBase predicate;
        private IPrincipalGrain self=null;
        private Guid workflowID;
        private IControllerGrain controllerGrain;
        private ulong sequenceNumber=0;
        private int currentPauseFlag=0;
        
        public virtual IWorkerGrain GetOperatorGrain(string extension)
        {
            throw new NotImplementedException();
        }

        public Task AddNextPrincipalGrain(IPrincipalGrain nextGrain)
        {
            nextPrincipalGrains.Add(nextGrain);
            return Task.CompletedTask;
        }

        public Task AddPrevPrincipalGrain(IPrincipalGrain prevGrain)
        {
            prevPrincipalGrains.Add(prevGrain);
            return Task.CompletedTask;
        }

        public async Task Init(IControllerGrain controllerGrain, Guid workflowID, Operator currentOperator)
        {
            this.controllerGrain=controllerGrain;
            this.workflowID=workflowID;
            this.operatorID=currentOperator.OperatorGuid;
            this.self=currentOperator.PrincipalGrain;
            this.predicate=currentOperator.Predicate;
            await BuildWorkerTopology();
            PassExtraParametersByPredicate(ref this.predicate);
            foreach(List<IWorkerGrain> grainList in operatorGrains)
            {
                foreach(IWorkerGrain grain in grainList)
                {
                    await grain.Init(grain,predicate,self);
                }
            }
        }


        protected virtual void PassExtraParametersByPredicate(ref PredicateBase predicate)
        {
            
        }

        public virtual async Task BuildWorkerTopology()
        {
            operatorGrains=Enumerable.Range(0, 1).Select(x=>new List<IWorkerGrain>()).ToList();
            //one-layer init
            for(int i=0;i<DefaultNumGrainsInOneLayer;++i)
            {
                IWorkerGrain grain=GetOperatorGrain(i.ToString());
                operatorGrains[0].Add(grain);
            }
            // for multiple-layer init, do some linking inside...
        }

        public async Task LinkWorkerGrains()
        {
            int count=0;
            foreach(IPrincipalGrain prevPrincipal in prevPrincipalGrains)
            {
                List<IWorkerGrain> prevOutputGrains=await prevPrincipal.GetOutputGrains();
                count+=prevOutputGrains.Count;
            }
            if(count>0)
            {
                foreach(IWorkerGrain grain in inputGrains)
                {
                    await grain.SetTargetEndFlagCount(count);
                }
            }

            if(nextPrincipalGrains.Count!=0)
            {
                foreach(IPrincipalGrain nextPrincipal in nextPrincipalGrains)
                {
                    ISendStrategy strategy = await nextPrincipal.GetInputSendStrategy();
                    for(int i=0;i<outputGrains.Count;++i)
                    {
                        await outputGrains[i].SetSendStrategy(operatorID,strategy);
                    }
                }
            }
            else
            {
                //last operator, build stream
                var streamProvider = GetStreamProvider("SMSProvider");
                var stream = streamProvider.GetStream<Immutable<PayloadMessage>>(workflowID,"OutputStream");
                ISendStrategy strategy=new SendToStream(stream);
                foreach(IWorkerGrain grain in outputGrains)
                    await grain.SetSendStrategy(workflowID,strategy);
            }
        }

        // protected async Task Link2Layers(Guid nextOperatorGuid, List<IWorkerGrain> currentLayer,List<IWorkerGrain> nextLayer)
        // {
        //     for(int i=0;i<currentLayer.Count;++i)
        //     {
        //         await currentLayer[i].AddNextGrainList(nextOperatorGuid,nextLayer);
        //     }
        // }

        public Task<List<IWorkerGrain>> GetInputGrains()
        {
            return Task.FromResult(inputGrains);
        }

        public Task<List<IWorkerGrain>> GetOutputGrains()
        {
            return Task.FromResult(outputGrains);
        }

        private string ReturnGrainIndentifierString(IPrincipalGrain grain)
        {
            string extension;
            return grain.GetPrimaryKey(out extension).ToString()+extension;
        }

        public virtual async Task Pause()
        {
            if(currentPauseFlag<inputGrains.Count && !isPaused)
            {
                currentPauseFlag++;
            }
            else
            {
                currentPauseFlag=0;
                if(isPaused)
                {
                    return;
                }
                isPaused = true;
                foreach(List<IWorkerGrain> grainList in operatorGrains)
                {
                    foreach(IWorkerGrain grain in grainList)
                    {
                        await grain.ProcessControlMessage(new Immutable<ControlMessage>(new ControlMessage(ReturnGrainIndentifierString(self),sequenceNumber,ControlMessage.ControlMessageType.Pause)));
                    }
                }
                sequenceNumber++;
                foreach(IPrincipalGrain next in nextPrincipalGrains)
                {
                    await SendPauseToNextPrincipalGrain(next,0);
                }
            }
        }

        private async Task SendPauseToNextPrincipalGrain(IPrincipalGrain nextGrain, int retryCount)
        {
            nextGrain.Pause().ContinueWith((t)=>
            {
                if(Utils.IsTaskTimedOutAndStillNeedRetry(t,retryCount))
                    SendPauseToNextPrincipalGrain(nextGrain,retryCount+1);
            });
        }

        public virtual async Task Resume()
        {
            if(!isPaused)
            {
                return;
            }
            foreach(IPrincipalGrain next in nextPrincipalGrains)
            {
                await SendResumeToNextPrincipalGrain(next,0);
            }
            isPaused = false;
            foreach(List<IWorkerGrain> grainList in operatorGrains)
            {
                foreach(IWorkerGrain grain in grainList)
                {
                    await grain.ProcessControlMessage(new Immutable<ControlMessage>(new ControlMessage(ReturnGrainIndentifierString(self),sequenceNumber,ControlMessage.ControlMessageType.Resume)));
                }
            }
            sequenceNumber++;
        }

        private async Task SendResumeToNextPrincipalGrain(IPrincipalGrain nextGrain, int retryCount)
        {
            nextGrain.Resume().ContinueWith((t)=>
            {
                if(Utils.IsTaskTimedOutAndStillNeedRetry(t,retryCount))
                    SendResumeToNextPrincipalGrain(nextGrain,retryCount+1);
            });
        }


        public virtual async Task Start()
        {
            foreach(IWorkerGrain grain in inputGrains)
            {
                 await grain.ProcessControlMessage(new Immutable<ControlMessage>(new ControlMessage(ReturnGrainIndentifierString(self),sequenceNumber,ControlMessage.ControlMessageType.Start)));
            }
            sequenceNumber++;
        }

        public virtual Task<ISendStrategy> GetInputSendStrategy()
        {
            return Task.FromResult(new RoundRobin(inputGrains,predicate.BatchingLimit) as ISendStrategy);
        }
    }
}
