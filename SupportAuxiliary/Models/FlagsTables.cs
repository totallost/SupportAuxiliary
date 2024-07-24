namespace SupportAuxiliary.Models
{
    public class FlagsTables
    {
        public List<Flags> Flags { get; set; }
        public List<Tables> Tables { get; set; }
    }
    public class Flags
    {
        public string FlagNumber { get; set; }
    }
    public class Tables
    {
        public string TableName { get; set; }
        public string TableNumber { get; set; }
    }
}
