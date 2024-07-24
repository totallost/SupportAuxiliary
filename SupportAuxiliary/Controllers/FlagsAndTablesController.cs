using Microsoft.AspNetCore.Mvc;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi.Legacy;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using SupportAuxiliary.Models;
using System.Text.RegularExpressions;
using SupportAuxiliary.Logger;
using Microsoft.IdentityModel.Tokens;

namespace SupportAuxiliary.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FlagsAndTablesController : ControllerBase
    {
        private readonly ILogger<FlagsAndTablesController> _logger;
        private List<Programs> _programsHo;
        private List<Programs> _programsFo;
        private List<Tables> _tablesFo;
        private List<Tables> _tablesHo;
        private TfvcHttpClient tfvcClient;

        public FlagsAndTablesController(ILogger<FlagsAndTablesController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetFlagsAndTables(int programNumber, string product)
        {
            Exception exception = new Exception();
            try
            {
                if (product == "FrontOffice")
                {
                    using (tfvcClient = ConnectToTFS().Result)
                    {
                        _programsFo = GetAllPrograms(ProgramType.FrontOffice).Result;
                        _tablesFo = GerAllTables(ProgramType.FrontOffice).Result;
                        var program = _programsFo.FirstOrDefault(p => p.ProgramNumber == programNumber);
                        if (program == null)
                        {
                            return NotFound();
                        }
                        FlagsTables flagsAndTables = await GetListsOfFlagsAndTables(program, product);
                        CleanMemoery(ProgramType.FrontOffice);
                        return Ok(flagsAndTables);
                    }
                }
                if (product == "HeadOffice")
                {
                    using (tfvcClient = ConnectToTFS().Result)
                    {
                        _programsHo = GetAllPrograms(ProgramType.HeadOffice).Result;
                        _tablesHo = GerAllTables(ProgramType.HeadOffice).Result;
                        var program = _programsHo.FirstOrDefault(p => p.ProgramNumber == programNumber);
                        if (program == null)
                        {
                            return NotFound();
                        }
                        FlagsTables flagsAndTables = await GetListsOfFlagsAndTables(program, product);
                        CleanMemoery(ProgramType.HeadOffice);
                        return Ok(flagsAndTables);
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                Logger.Logger.WriteToLog(ex.Message);
                _logger.LogError(ex, "Error while trying to get flags and tables");
            }
            return BadRequest("something went wrong, probably your face: "+exception.Message);
        }
        private void CleanMemoery(ProgramType programType)
        {
            tfvcClient.Dispose();
            if(programType == ProgramType.FrontOffice)
            {
                _programsFo.Clear();
                _programsFo = null;
                _tablesFo.Clear();
                _tablesFo = null;
            }
            if(programType == ProgramType.HeadOffice)
            {
                _programsHo.Clear();
                _programsHo = null;
                _tablesHo.Clear();
                _tablesHo = null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        private async Task<FlagsTables> GetListsOfFlagsAndTables(Programs program, string product)
        {
            string projectName = "Retail360.IL";
            string[] FileContent;
            try
            {
                FileContent = GetItemFromTFS(tfvcClient, projectName, program.ProgramPath, program.ProgramName).Result;
            }
            catch (Exception ex)
            {
                try
                {
                    program.ProgramPath = program.ProgramPath.Replace("Core.", ".");
                    program.ProgramName = program.ProgramName.Replace("Core.", ".");
                    FileContent = GetItemFromTFS(tfvcClient, projectName, program.ProgramPath, program.ProgramName).Result;
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error while trying to get file content");
                    throw;
                }
                _logger.LogError(ex, "Error while trying to get file content");
            }
            FlagsTables flagsTables = new FlagsTables();
            flagsTables.Flags = new List<Flags>();
            flagsTables.Tables = new List<Tables>();
            string patternTables = @"\.Model\.(\w+)";
            string patternFlags =  @"\.SettingNumber\.IsEqualTo\((\d+)\)";
            string patternFlags2 = @"\.SettingNumber\.BindEqualTo\((\d+)\)";
            string patternFlags3 = @"\.Definition\.Create\((\d+)\)";
            if (product=="HeadOffice")
            {
                patternTables = @"\.Model\.(\w+)";
                patternFlags = @"\.MsprAgdra\.IsEqualTo\((\d+)\)";
                patternFlags2 = @"\.MsprAgdra\.BindEqualTo\((\d+)\)";
            }
            foreach (var line in FileContent)
            {
                await SeachMatchingPatterns(line, patternTables, flagsTables, "table", product);
                await SeachMatchingPatterns(line, patternFlags, flagsTables, "flag", product);
                await SeachMatchingPatterns(line, patternFlags2, flagsTables, "flag", product);
                await SeachMatchingPatterns(line, patternFlags3, flagsTables, "flag", product);
            }
            FileContent = null;
            return flagsTables;
        }

        private async Task SeachMatchingPatterns(string line, string pattern, FlagsTables flagsTables, string tableOrFlag, string product)
        {
            Match matchTables = Regex.Match(line, pattern);
            if (matchTables.Success & tableOrFlag == "table")
            {
                string TableNumber= string.Empty;
                if(product == "FrontOffice")
                {
                    TableNumber = _tablesFo.FirstOrDefault(t => t.TableName == matchTables.Groups[1].Value)?.TableNumber;
                }
                if (product == "HeadOffice")
                {
                    TableNumber = _tablesHo.FirstOrDefault(t => t.TableName == matchTables.Groups[1].Value)?.TableNumber;
                }
                Tables table = new Tables
                {
                    TableName = matchTables.Groups[1].Value,
                    TableNumber = TableNumber.IsNullOrEmpty()? "???": TableNumber
                };
                if (!flagsTables.Tables.Any(t => t.TableNumber == table.TableNumber))
                {
                    flagsTables.Tables.Add(table);
                }
            }
            if(matchTables.Success & tableOrFlag == "flag")
            {
                Flags flag = new Flags
                {
                    FlagNumber = matchTables.Groups[1].Value
                };
                if (!flagsTables.Flags.Any(f => f.FlagNumber == flag.FlagNumber))
                {
                    flagsTables.Flags.Add(flag);
                }
            }
        }

        private async Task<List<Programs>> GetAllPrograms(ProgramType programType)
        {
            string projectName = "Retail360.IL";
            string AllProgramsPath = $"$/Retail360.IL/Dev/{programType.ToString()}/Retail360.IL.{programType.ToString()}.App/ApplicationPrograms.cs";
            string fileName = projectName.Split('/').LastOrDefault() + ".cs";
            string[] allLines = await GetItemFromTFS(tfvcClient, projectName, AllProgramsPath, fileName);
            return CreateList(allLines, programType);
        }

        private List<Programs> CreateList(string[] allLines, ProgramType programType)
        {
            List<Programs> programs = new List<Programs>();
            foreach (var line in allLines)
            {
                // Match the first set of parentheses and capture its content
                Match match = Regex.Match(line, @"Add\((.*?)\);");
                if (match.Success)
                {
                    // Extract the content inside the brackets
                    string content = match.Groups[1].Value;

                    // Split the content by commas
                    string[] values = content.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(value => value.Trim().Trim('"'))
                                             .ToArray();

                    // Ensure we have enough values to construct Programs object
                    if (values.Length > 4)
                    {
                        string _programName = values[4].Replace(")","").Split('.')[values[4].Split('.').Length - 1] + "Core.cs";
                        string _programPath = $"$/Retail360.IL/Dev/{programType.ToString()}/" + values[3]+"/"+ _programName;
                        Programs program = new Programs
                        {
                            ProgramNumber = int.Parse(values[0].Trim()),
                            ProgramName = _programName.Trim(),
                            ProgramPath = _programPath,
                            ProgramHebrewName = values[1].Trim()
                        };
                        programs.Add(program);
                    }
                    else
                    {
                        string _programName = values[3]
                            .Replace(")","")
                            .Split('.')[values[3].Split('.').Length - 1] + "Core.cs";
                        string _programPath = $"$/Retail360.IL/Dev/{programType.ToString()}/"+ 
                            $"Retail360.IL.{programType.ToString()}." + 
                            values[3].Replace("typeof(", "")
                            .Replace(")","")
                            .Replace("( => ","")
                            .Split('.')[0] + "/" + 
                            _programName;
                        Programs program = new Programs
                        {
                            ProgramNumber = int.Parse(values[0].Trim()),
                            ProgramName = _programName.Trim(),
                            ProgramPath = _programPath,
                            ProgramHebrewName = values[1].Trim()
                        };
                        programs.Add(program);
                    }
                }
            }
            return programs;
        }

        private async Task<string[]> GetFileContent(TfvcHttpClient tfvcClient, TfvcItem allProgramsItem)
        {
            try
            {
                if (allProgramsItem == null)
                {
                    throw new Exception("File not found");
                }
                using (Stream fileContentStream = await tfvcClient.GetItemContentAsync(allProgramsItem.Path))
                {

                    string fileContent;
                    using (StreamReader reader = new StreamReader(fileContentStream))
                    {
                        fileContent = reader.ReadToEnd();
                    }
                    // Split the fileContent into lines
                    string[] lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    return lines;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trying to get file content");
                throw;
            }
        }

        private async Task<string[]> GetItemFromTFS(TfvcHttpClient tfvcClient, string projectName, string projectPath, string fileName)
        {
            TfvcItem allProgramsItem;
            try
            {
                allProgramsItem = await tfvcClient.GetItemAsync(project: projectName, path: projectPath, fileName: fileName, null, null, null, null, null, null, default);
            }
            catch (Exception ex)
            {
                allProgramsItem = await FIndFileInTFS(tfvcClient, projectName, projectPath, fileName);
            }
            //get string array of all the programs
            return await GetFileContent(tfvcClient, allProgramsItem);
        }

        private async Task<TfvcHttpClient> ConnectToTFS()
        {
            string tfsUrl = "http://tlvwvtfsproxy2:8080/tfs/retail360";
            string pat = "j23vtm2xzslyw3edx7tcdnpqexjiyggep2kxygssvye4svi6z3na";
            // Optionally, credentials if needed
            VssConnection connection = new VssConnection(new Uri(tfsUrl), new VssAadCredential(string.Empty, pat));
            // Get the TFVC version control client
            return  await connection.GetClientAsync<TfvcHttpClient>();
        }

        private async Task<TfvcItem> FIndFileInTFS(TfvcHttpClient tfvcClient, string projectName, string projectPath, string fileName)
        {
            try
            {
                var allProgramsItem = await tfvcClient.GetItemsAsync(scopePath: "$/Retail360.IL/Dev/HeadOffice/"+fileName, recursionLevel: Microsoft.TeamFoundation.SourceControl.WebApi.VersionControlRecursionType.Full);
                foreach (var item in allProgramsItem)
                {
                    if (item.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trying to find file in TFS");
            }
            return null;
        }

        private async Task<List<Tables>> GerAllTables(ProgramType programType)
        {
            string projectName = "Retail360.IL";
            string AllTablesPath = $"$/Retail360.IL/Dev/{programType.ToString()}/Retail360.IL.{programType.ToString()}.App/ApplicationEntities.cs";
            string fileName = projectName.Split('/').LastOrDefault() + ".cs";
            string[] allLines = await GetItemFromTFS(tfvcClient, projectName, AllTablesPath, fileName);
            return CreateTableList(allLines, programType);
        }
        private List<Tables> CreateTableList(string[] allLines, ProgramType programType)
        {
            List<Tables> tables = new List<Tables>();
            foreach (var line in allLines)
            {
                // Match the first set of parentheses and capture its content
                Match match = Regex.Match(line, @"Add\((.*?)\);");
                if (match.Success)
                {
                    // Extract the content inside the brackets
                    string content = match.Groups[1].Value;

                    // Split the content by commas
                    string[] values = content.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(value => value.Trim().Trim('"'))
                                             .ToArray();
                    string _TableName = values[2].Replace(")", "").Split('.')[values[2].Split('.').Length - 1];
                    string _TableNumber = values[0].Trim();
                    Tables table = new Tables
                    {
                        TableName = _TableName.Trim(),
                        TableNumber = _TableNumber.Trim()
                    };
                    tables.Add(table);
                }
            }
            return tables;
        }
    }
}
