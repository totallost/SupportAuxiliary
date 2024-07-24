namespace SupportAuxiliary.Models
{
    public class Programs
    {
        public int ProgramNumber { get; set; }
        public string ProgramPath { get; set; }
        public string ProgramName { get; set; }
        public string ProgramHebrewName { get; set; }
    }
    public enum ProgramType
    {
        FrontOffice,
        HeadOffice
    }
}
