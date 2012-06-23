using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EnumGenerator
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        /// <param name="arguments">設定ファイル名</param>
        [STAThread]
        static void Main(string[] arguments)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string settingFile = arguments.FirstOrDefault();

            if (settingFile == null)
            {
                return;
            }

            Dictionary<string, string> settingDictionary = new Dictionary<string, string>();

            using(StreamReader reader = new StreamReader(settingFile, Encoding.UTF8, false))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    Match match = Regex.Match(line, @"\[(?<Key>.*)\](?<Value>.*)");
                    if (match.Success == true)
                    {
                        settingDictionary.Add(match.Groups["Key"].Value, match.Groups["Value"].Value);
                    }
                }
            }

            string clrVersion = settingDictionary["ClrVersion"];
            string assemblyName = settingDictionary["Assembly"];
            string databaseProvider = settingDictionary["DbProvider"];
            string connectionString = settingDictionary["ConnectionString"];

            string enumSql = File.ReadAllText(settingDictionary["Sql"], Encoding.UTF8);
            string assemblyNameNoExtention = Path.GetFileNameWithoutExtension(assemblyName);

            DataTable enumTable = new DataTable();

            DbProviderFactory providerFactory = DbProviderFactories.GetFactory(databaseProvider);
            
            using (DbConnection connection = providerFactory.CreateConnection())
            using (DbDataAdapter adapter = providerFactory.CreateDataAdapter())
            using (DbCommand command = connection.CreateCommand())
            {
                connection.ConnectionString = connectionString;

                adapter.SelectCommand = command;
                command.CommandText = enumSql;

                adapter.Fill(enumTable);
            }

            var mEnumViewOptionGroupRows = enumTable.Rows.Cast<DataRow>()
                .GroupBy(m => 
                    new{ 
                        EnumType = Convert.ToString(m["EnumType"]),
                        EnumSummary = Convert.ToString(m["EnumTypeSummary"]),
                        Flags = Convert.ToBoolean(Convert.ToInt32(Convert.ToString(m["EnumTypeFlags"])))
                    });

            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

            try
            {
                using (StreamWriter writer = new StreamWriter(tempPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("namespace {0}{{", assemblyNameNoExtention);
                    foreach (var mEnumViewOptionGroup in mEnumViewOptionGroupRows)
                    {
                        var key = mEnumViewOptionGroup.Key;
                        writer.WriteLine("/// <summary>");
                        using (StringReader typeSummaryReader = new StringReader(key.EnumSummary))
                        {
                            for (string line = typeSummaryReader.ReadLine(); line != null; line = typeSummaryReader.ReadLine())
                            {
                                writer.WriteLine("/// {0}", line);
                            }
                        }
                        writer.WriteLine("/// </summary>");
                        if (key.Flags == true)
                        {
                            writer.WriteLine("[Flags]");
                        }
                        writer.WriteLine("public enum {0}{{", key.EnumType);

                        foreach (DataRow row in mEnumViewOptionGroup.ToArray())
                        {
                            writer.WriteLine("/// <summary>");
                            using (StringReader summaryReader = new StringReader(Convert.ToString(row["EnumSummary"])))
                            {
                                for (string line = summaryReader.ReadLine(); line != null; line = summaryReader.ReadLine())
                                {
                                    writer.WriteLine("/// {0}", line);
                                }
                            }
                            writer.WriteLine("/// </summary>");
                            writer.WriteLine("{0} = {1},", row["EnumName"], row["EnumValue"]);
                        }
                        writer.WriteLine("}");
                    }
                    writer.WriteLine("}");
                }

                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.Arguments = string.Format("/target:library /out:{0} {1} /doc:{2}.xml", assemblyName, tempPath, assemblyNameNoExtention);
                processStartInfo.FileName = Path.Combine(Path.Combine(@"C:\Windows\Microsoft.NET\Framework", clrVersion), "csc.exe");
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;
                using (Process process = Process.Start(processStartInfo))
                using (StreamReader reader = process.StandardError)
                using (StreamReader outputReader = process.StandardOutput)
                {
                    process.WaitForExit();
                    Console.WriteLine(".net framework コンパイラを実行しています。");
                    Console.Write(outputReader.ReadToEnd());
                }
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
