using Engine.Breakpoint.GlobalBreakpoint;
using Engine.DeploySemantics;
using Engine.LinkSemantics;
using Engine.OperatorImplementation.Common;
using Orleans;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TexeraUtilities;

namespace Engine.OperatorImplementation.FaultTolerance
{
    public class HashBasedMaterializerOperator : Operator
    {
        private Guid id;
        private int numBuckets;
        private string serializedHashFunc;
        public HashBasedMaterializerOperator(Guid id,int numBuckets,string serializedHashFunc) : base()
        {
            this.id = id;
            this.numBuckets = numBuckets;
            this.serializedHashFunc = serializedHashFunc;
            string dirName = "/amber-tmp/"+id;
            ("hadoop fs -mkdir hdfs://10.138.0.2:8020"+dirName).Bash();
            for(int i=0;i<numBuckets;++i)
            {
                ("hadoop fs -mkdir hdfs://10.138.0.2:8020"+dirName+"/"+i).Bash();
            }
        }

        public override void AssignBreakpoint(List<WorkerLayer> layers, Dictionary<IWorkerGrain, WorkerState> states, GlobalBreakpointBase breakpoint)
        {
            throw new System.NotImplementedException();
        }

        public override Pair<List<WorkerLayer>, List<LinkStrategy>> GenerateTopology()
        {
            return new Pair<List<WorkerLayer>,List<LinkStrategy>>
            (
                new List<WorkerLayer>
                {
                    new ProcessorWorkerLayer("materializer.main",numBuckets,(i)=>new HashBasedMaterializer(id,i,numBuckets,serializedHashFunc),null)
                },
                new List<LinkStrategy>
                {

                }
            );
        }

        public override string GetHashFunctionAsString(Guid from)
        {
            throw new System.NotImplementedException();
        }

        public override bool IsStaged(Operator from)
        {
            return true;
        }
    }
}