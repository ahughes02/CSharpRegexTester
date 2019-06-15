using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace CSharpRegexTester
{
    [Serializable]
    public class RegexTesterConfigurationFile
    {
        public List<string> ExpressionList { get; set; }
        public string CurrentExpression { get; set; }
        public string CurrentReplacementText { get; set; }
        public string CurrentTestData { get; set; }

        private const string CFG_FILE_NAME = "RegExTester.cfg";

        public static RegexTesterConfigurationFile Load()
        {
            RegexTesterConfigurationFile config = null;

            if (File.Exists(CFG_FILE_NAME))
            {
                var serializer = new BinaryFormatter();
                FileStream configStream = File.OpenRead(CFG_FILE_NAME);
                if (configStream != null)
                {
                    try
                    {
                        config = (RegexTesterConfigurationFile)serializer.Deserialize(configStream);
                    }
                    catch (Exception) { }
                    configStream.Close();
                }
            }

            return config;
        }

        public void Save()
        {
            var serializer = new BinaryFormatter();
            FileStream configStream;

            if (File.Exists(CFG_FILE_NAME))
            {
                configStream = File.OpenWrite(CFG_FILE_NAME);
            }
            else
            {
                configStream = File.Create(CFG_FILE_NAME);
            }

            if (configStream != null)
            {
                serializer.Serialize(configStream, this);
                configStream.Flush();
                configStream.Close();
            }
        }
    }
}
