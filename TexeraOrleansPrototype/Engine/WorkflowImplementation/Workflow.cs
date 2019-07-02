using System.Collections.Generic;
using Engine.OperatorImplementation.Common;
using Engine.Controller;
using Orleans;
using System.Threading.Tasks;
using System;
using Orleans.Runtime;
using TexeraUtilities;

namespace Engine.WorkflowImplementation
{
    public class Workflow
    {
        public HashSet<Operator> StartOperators = new HashSet<Operator>();
        public HashSet<Operator> AllOperators;
        public HashSet<Operator> EndOperators=new HashSet<Operator>();
        public readonly Guid WorkflowID;
        private ICreatorGrain workflowControllerGrain=null;

        public Workflow(Guid workflowID)
        {
            this.WorkflowID=workflowID;
            
        }

        public void InitializeOperatorSet(HashSet<Operator> allOperators)
        {
            this.AllOperators=allOperators;
            foreach(Operator o in allOperators)
            {
                if(o.GetAllOutOperators().Count==0)
                    EndOperators.Add(o);
                if(o.IsStartOperator)
                    StartOperators.Add(o);
            }
        }

        public async Task Init(IGrainFactory factory)
        {
            workflowControllerGrain=factory.GetGrain<ICreatorGrain>(new Guid());
            foreach(Operator o in AllOperators)
            {
                o.SetPrincipalGrain(factory);
            }
            RequestContext.Set("targetSilo",Constants.ClientIPAddress);
            await workflowControllerGrain.Init(null,WorkflowID,AllOperators);
        }

        public Task Pause()
        {
            workflowControllerGrain.Pause(StartOperators);
            return Task.CompletedTask;
        }

        public async Task Resume()
        {
            await workflowControllerGrain.Resume(StartOperators);
            return;
        }

        public async Task Deactivate()
        {
            List<Task> taskList=new List<Task>();
            foreach(Operator o in AllOperators)
            {
                taskList.Add(o.Deactivate());
            }
            await Task.WhenAll(taskList);
        }


        public Guid GetStreamGuid()
        {
            return WorkflowID;
        }
    }
}