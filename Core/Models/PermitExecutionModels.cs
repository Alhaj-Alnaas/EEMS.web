using System;
using System.Collections.Generic;
using static Core.Enums.BaseEnums;

namespace Core.Models
{
    public class PermitExecutionStep
    {
        public bool Completed { get; set; }
        public MovementDirection Direction { get; set; }
        public string ActionLabel { get; set; } = "";
        public string Hint { get; set; } = "";
        public bool ClosesPermit { get; set; }
        public string Progress { get; set; } = "";
    }

    public class PermitExecutionEventRow
    {
        public DateTime When { get; set; }
        public string Action { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Gate { get; set; } = "";
    }

    public class OpenPermitAlert
    {
        public Guid PermitId { get; set; }
        public string Number { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Waiting { get; set; } = "";
        public DateTime Since { get; set; }
        public int HoursOpen { get; set; }
    }
}
