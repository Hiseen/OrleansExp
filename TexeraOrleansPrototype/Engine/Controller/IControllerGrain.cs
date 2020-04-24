using Orleans;
using System.Threading.Tasks;
using System.Collections.Generic;
using Engine.OperatorImplementation.Common;
using System;
using Engine.Breakpoint.GlobalBreakpoint;
using Orleans.Runtime;

namespace Engine.Controller
{
    public interface IControllerGrain : IGrainWithGuidKey
    {
        Task<SiloAddress> Init(IControllerGrain self, string plan,bool checkpointActivated);
        Task Start();
        Task Pause();
        Task Resume();
        Task Deactivate();
        Task OnPrincipalRunning(IPrincipalGrain sender);
        Task OnPrincipalPaused(IPrincipalGrain sender);
        Task OnPrincipalCompleted(IPrincipalGrain sender);
        Task SetBreakpoint(Guid operatorID,GlobalBreakpointBase breakpoint);
        Task OnBreakpointTriggered(string report);
        Task<int> GetNumberOfOutputGrains();
        Task OnPrincipalReceivedAllBatches(IPrincipalGrain sender);
    }
}