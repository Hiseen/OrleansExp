﻿using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TexeraUtilities;
using Orleans.Streams;
using System.Diagnostics;
using Engine.OperatorImplementation.MessagingSemantics;
using Engine.OperatorImplementation.SendingSemantics;
using System.Threading;
using System.Linq;
using System.Collections.Concurrent;

namespace Engine.OperatorImplementation.Common
{
    public class Pair<T, U> 
    {
        public Pair(T first, U second) 
        {
            this.First = first;
            this.Second = second;
        }

        public T First { get; set; }
        public U Second { get; set; }
    };


    public class WorkerGrain : Grain, IWorkerGrain
    {
        protected PredicateBase predicate = null;
        protected bool isPaused = false;
        protected List<Immutable<PayloadMessage>> pausedMessages = new List<Immutable<PayloadMessage>>();
        protected IPrincipalGrain principalGrain;
        protected IWorkerGrain self = null;
        private IOrderingEnforcer orderingEnforcer = Utils.GetOrderingEnforcerInstance();
        private Dictionary<Guid,ISendStrategy> sendStrategies = new Dictionary<Guid, ISendStrategy>();
        protected Dictionary<string,int> inputInfo;
        protected Queue<Action> actionQueue=new Queue<Action>();
        protected int currentIndex=0;
        protected int currentEndFlagCount=int.MaxValue;
        protected ConcurrentQueue<TexeraTuple> outputTuples=new ConcurrentQueue<TexeraTuple>();
        protected bool isFinished=false;

        public virtual Task Init(IWorkerGrain self, PredicateBase predicate, IPrincipalGrain principalGrain)
        {
            this.self=self;
            this.principalGrain=principalGrain;
            this.predicate=predicate;
            return Task.CompletedTask;
        }
        
        public Task Process(Immutable<PayloadMessage> message)
        {
            if(isPaused)
            {
                pausedMessages.Add(message);
                return Task.CompletedTask;
            }
            if(orderingEnforcer.PreProcess(message))
            {
                bool isEnd=message.Value.IsEnd;
                List<TexeraTuple> batch=message.Value.Payload;
                orderingEnforcer.CheckStashed(ref batch,ref isEnd, message.Value.SenderIdentifer);  
                var orleansScheduler=TaskScheduler.Current;
                Action action=async ()=>
                {
                    BeforeProcessBatch(message,orleansScheduler);
                    if(batch!=null)
                    {
                        ProcessBatch(batch);
                    }
                    if(isPaused)
                    {
                        return;
                    }
                    currentIndex=0;
                    if(isEnd)
                    {
                        inputInfo[message.Value.SenderIdentifer.Split(' ')[0]]--;
                        currentEndFlagCount--;
                    }
                    AfterProcessBatch(message,orleansScheduler);
                    await Task.Factory.StartNew(()=>{MakePayloadMessagesThenSend();},CancellationToken.None,TaskCreationOptions.None,orleansScheduler);
                    lock(actionQueue)
                    {
                        actionQueue.Dequeue();
                        if(!isPaused && actionQueue.Count>0)
                        {
                            Task.Run(actionQueue.Peek());
                        }
                    }
                };
                lock(actionQueue)
                {
                    actionQueue.Enqueue(action);
                    if(actionQueue.Count==1)
                    {
                        Task.Run(action);
                    }
                }
            }
            return Task.CompletedTask;
        }

        protected void MakePayloadMessagesThenSend()
        {
            foreach(ISendStrategy strategy in sendStrategies.Values)
            {
                strategy.Enqueue(outputTuples);
                string identifer=ReturnGrainIndentifierString(self);
                strategy.SendBatchedMessages(identifer);
            }
            outputTuples=new ConcurrentQueue<TexeraTuple>();
            if(currentEndFlagCount==0 && actionQueue.Count==1)
            {
                isFinished=true;
                MakeLastPayloadMessageThenSend();
            }
        }

        private void MakeLastPayloadMessageThenSend()
        {
            MakeFinalOutputTuples();
            foreach(ISendStrategy strategy in sendStrategies.Values)
            {
                strategy.Enqueue(outputTuples);
                string identifer=ReturnGrainIndentifierString(self);
                strategy.SendBatchedMessages(identifer);
                strategy.SendEndMessages(identifer);
            }
            outputTuples= new ConcurrentQueue<TexeraTuple>();
        }


        protected virtual void BeforeProcessBatch(Immutable<PayloadMessage> message, TaskScheduler orleansScheduler)
        {

        }

        protected virtual void AfterProcessBatch(Immutable<PayloadMessage> message, TaskScheduler orleansScheduler)
        {

        }
        protected void ProcessBatch(List<TexeraTuple> batch)
        {
            for(;currentIndex<batch.Count;++currentIndex)
            {
                if(isPaused)
                {
                    return;
                }
                ProcessTuple(batch[currentIndex]);
            }
        }

        protected virtual void ProcessTuple(TexeraTuple tuple)
        {

        }

        public Task ProcessControlMessage(Immutable<ControlMessage> message)
        {
            List<ControlMessage.ControlMessageType> executeSequence = orderingEnforcer.PreProcess(message);
            if(executeSequence!=null)
            {
                orderingEnforcer.CheckStashed(ref executeSequence,message.Value.SenderIdentifer);
                foreach(ControlMessage.ControlMessageType type in executeSequence)
                {
                    switch(type)
                    {
                        case ControlMessage.ControlMessageType.Pause:
                            Pause();
                            break;
                        case ControlMessage.ControlMessageType.Resume:
                            Resume();
                            break;
                        case ControlMessage.ControlMessageType.Start:
                            Start();
                            break;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task ReceivePayloadMessage(Immutable<PayloadMessage> message)
        {
            //Console.WriteLine(MakeIdentifier(self) + " received message from "+message.Value.SenderIdentifer+"with seqnum "+message.Value.SequenceNumber);
            SendPayloadMessageToSelf(message,0);
            return Task.CompletedTask;
        }


        private void SendPayloadMessageToSelf(Immutable<PayloadMessage> message, int retryCount)
        {
            self.Process(message).ContinueWith((t)=>
            {  
                if(Utils.IsTaskTimedOutAndStillNeedRetry(t,retryCount))
                    SendPayloadMessageToSelf(message, retryCount + 1); 
            });
        }

        protected virtual void MakeFinalOutputTuples()
        {
            
        }


        public string ReturnGrainIndentifierString(IWorkerGrain grain)
        {
            //string a="Engine.OperatorImplementation.Operators.OrleansCodeGen";
            string extension;
            //grain.GetPrimaryKey(out extension);
            return grain.GetPrimaryKey(out extension).ToString()+" "+extension;
        }

        protected virtual void Pause()
        {
            isPaused=true;
        }

        protected virtual void Resume()
        {
            isPaused=false;
            if(isFinished)
            {
                return;
            }
            if(actionQueue.Count>0)
            {
               
                new Task(actionQueue.Peek()).Start(TaskScheduler.Default);
            }
            foreach(Immutable<PayloadMessage> message in pausedMessages)
            {
                SendPayloadMessageToSelf(message,0);
            }
            pausedMessages=new List<Immutable<PayloadMessage>>();
        }

       
        protected virtual void Start()
        {
            throw new NotImplementedException();
        }

        public Task SetInputInformation(Dictionary<string,int> inputInfo)
        {
            currentEndFlagCount=inputInfo.Values.Sum();
            this.inputInfo=inputInfo;
            return Task.CompletedTask;
        }

        public Task Generate()
        {
            if(!isPaused)
            {
                GenerateTuples();
                if(!isFinished || outputTuples.Count>0)
                {
                    MakePayloadMessagesThenSend();
                    StartGenerate(0);
                }
                else
                {
                    foreach(ISendStrategy strategy in sendStrategies.Values)
                    {
                        string identifer=ReturnGrainIndentifierString(self);
                        strategy.SendEndMessages(identifer);
                    }
                }
            }
            return Task.CompletedTask;
        }

        protected virtual void GenerateTuples()
        {
            
        }

        protected void StartGenerate(int retryCount)
        {
            self.Generate().ContinueWith((t)=>
            {
                if(Utils.IsTaskTimedOutAndStillNeedRetry(t,retryCount))
                {
                    StartGenerate(retryCount);
                }
            });
        }

        public Task SetSendStrategy(Guid operatorGuid,ISendStrategy sendStrategy)
        {
            sendStrategies[operatorGuid]=sendStrategy;
            return Task.CompletedTask;
        }
    }
}
