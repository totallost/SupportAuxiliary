namespace SupportAuxiliary.Logger
{
    public class Logger
    {
        public static string LogPath { get; set; }
        public static void WriteToLog(string infoString)
        {
            LogPath = @"C:\temp\SupportAuxiliaryLog.txt";

            if (File.Exists(LogPath))
            {
                using (StreamWriter sw = File.AppendText(LogPath))
                {
                    sw.WriteLine(DateTime.Now + ":" + " " + infoString);
                }
            }
            else
            {
                File.Create(LogPath).Close();
                using (StreamWriter sw = File.CreateText(LogPath))
                {
                    sw.WriteLine(DateTime.Now + ":" + " " + infoString);
                }
            }
        }
    }
}
