using System;
using System.Collections.Generic;

namespace TcpServerApp.Models.Orchestration
{
    public class DisplayAreaInfo
    {
        public string Token { get; set; } = "";
        public string Name { get; set; } = "";
        public int DisplayId { get; set; }
        public int FeatureId { get; set; }
        public int RootDisplayAreaId { get; set; }
        public bool IsRoot { get; set; }
        public List<WindowContainerInfo> Children { get; set; } = new();
    }

    public class WindowContainerInfo
    {
        public string Token { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // Task, DisplayArea, TaskFragment
        public float Alpha { get; set; } = 1.0f;
        public bool IsVisible { get; set; } = true;
        public int Index { get; set; } // Stack depth
        public string AuditMetadata { get; set; } = ""; 
        public Rect Bounds { get; set; }
        public int WindowingMode { get; set; }
        public int DisplayId { get; set; }

    }

    public struct Rect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

    }
}
