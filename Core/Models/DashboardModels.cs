namespace Core.Models
{
    public class LeaderDashboard
    {
        public bool IsLeader { get; set; }
        public bool IsSecurity { get; set; }
        public string LeaderName { get; set; } = "";
        public string UnitCode { get; set; } = "";
        public string UnitName { get; set; } = "";
        public DateTime Day { get; set; }
        public DivisionBoard? Division { get; set; }
        public SecurityBoard? Security { get; set; }
    }

    public class DivisionBoard
    {
        public int RegisteredEmployees { get; set; }
        public int AbsentToday { get; set; }
        public int LateAfterNine { get; set; }
        public int LeftBeforeThree { get; set; }
        public int InExecution { get; set; }
        public int AwaitingLeader { get; set; }
        public int Executed { get; set; }
    }

    public class SecurityBoard
    {
        public int Employees { get; set; }
        public int PresentToday { get; set; }
        public int AbsentToday { get; set; }
        public int LateAfterNine { get; set; }
        public int LeftBeforeThree { get; set; }
        public int LeftAfterThree { get; set; }
        public int PermitsTotal { get; set; }
        public int Materials { get; set; }
        public int Visitors { get; set; }
        public int Vehicles { get; set; }
        public int EmployeeAccess { get; set; }
        public int ActivePermits { get; set; }
        public int OpenPermits { get; set; }
        public int PendingPermits { get; set; }
        public int ExecutedPermits { get; set; }
        public int Readers { get; set; }
        public int ReadersOnline { get; set; }
        public int ReadersOffline { get; set; }
        public int ReadersFaulty { get; set; }
        public int Gates { get; set; }
        public int ActiveGates { get; set; }
        public int GatesUsedToday { get; set; }
        public int MovementsToday { get; set; }
    }
}
