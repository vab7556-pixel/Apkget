using System;

namespace TcpServerApp.Models.Orchestration
{
    public enum OrchestrationCommandType
    {
        Reparent,
        SetBounds,
        SetVisibility,
        SetAlpha,
        CreateTaskDisplayArea,
        DeleteTaskDisplayArea
    }

    public class WindowContainerCommand
    {
        public OrchestrationCommandType Type { get; set; }
        public string TargetToken { get; set; }
        public string? ParentToken { get; set; }
        public Rect? NewBounds { get; set; }
        public bool? NewVisibility { get; set; }
        public float? NewAlpha { get; set; }
    }
}
