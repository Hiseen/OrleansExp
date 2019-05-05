using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Orleans.Concurrency;
using Engine.OperatorImplementation.Common;
using TexeraUtilities;

namespace Engine.OperatorImplementation.Operators
{
    public class ScanOperatorGrain : WorkerGrain, IScanOperatorGrain
    {
        private ulong start,end,tuple_counter=0;
        private ScanStreamReader reader;
        public static int GenerateLimit=1000;
        private string separator;

        protected override void Start()
        {
            StartGenerate(0);
            Console.WriteLine("start");
        }

        protected override void Resume()
        {
            isPaused=false;
            if(isFinished)
            {
                return;
            }
            StartGenerate(0);
        }

        protected override void GenerateTuples()
        {
            int i=0;
            while(i<GenerateLimit)
            {
                if(start>end)
                {
                    isFinished=true;
                    break;
                }
                TexeraTuple tuple;
                if(ReadTuple(out tuple))
                {
                    outputTuples.Add(tuple);
                    i++;
                }
                if(reader.IsEOF())
                {
                    isFinished=true;
                    break;
                }
            }
        }

        

        public override Task Init(IWorkerGrain self, PredicateBase predicate, IPrincipalGrain principalGrain)
        {
            base.Init(self,predicate,principalGrain);
            ulong filesize=((ScanPredicate)predicate).FileSize;
            separator=((ScanPredicate)predicate).Separator;
            string extensionKey = "";
            Guid key = this.GetPrimaryKey(out extensionKey);
            ulong i=UInt64.Parse(extensionKey);
            ulong num_grains=(ulong)((ScanPredicate)predicate).NumberOfGrains;
            ulong partition=filesize/num_grains;
            ulong start_byte=i*partition;
            ulong end_byte=num_grains-1==i?filesize:(i+1)*partition;
            reader=new ScanStreamReader(((ScanPredicate)predicate).File);
            if(!reader.GetFile(start_byte))
                return Task.FromException(new Exception("unable to get file"));
            start=start_byte;
            end=end_byte;
            if(start!=0)
                start+=reader.TrySkipFirst();
            Console.WriteLine("Init: start byte: "+start.ToString()+" end byte: "+end.ToString());
            return Task.CompletedTask;
        }

        


        private bool ReadTuple(out TexeraTuple tx)
        {
            try
            {
                ulong ByteCount;
                string res = reader.ReadLine(out ByteCount);
                start += ByteCount;
                if (reader.IsEOF())
                {
                    start = end + 1;
                    tx=null;
                    return false;
                }
                try
                {
                    tx=new TexeraTuple(res.Split(separator));
                    ++tuple_counter;
                    return true;
                }
                catch
                {
                    tx=null;
                    Console.WriteLine("Failed to parse the tuple");
                    return false;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("EXCEPTION in Reading Tuples from File - "+ ex.ToString());
                Console.WriteLine("start_offset: "+start.ToString()+" end_offset: "+end.ToString());
                if(!reader.GetFile(start))throw new Exception("Reading Tuple: Cannot Get File");
                tx=null;
                return false;
            }
        }
        
    }
}