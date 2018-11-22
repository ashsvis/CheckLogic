using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Specialized;
using System.Collections;

namespace CheckLogic
{
    /// <summary>
    /// Slower implementation of ini handling. File is not stored in the memory for extended period of time, 
    /// all changes are instantly written to the file, all reads open the file for reading again. Does not use unmanaged code.
    /// </summary>
    public class IniFile : BaseIni
    {

        /// <summary>
        /// Initializes new TIniFile instance
        /// </summary>
        /// <param name="name"></param>
        public IniFile(string name)
        {
            FileName = name;
        }


        public override bool KeyExists(string section, string ident)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        public override bool SectionExists(string section)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        #region Read/write basic values
        /// <summary>
        /// Read string value from the file
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        public override string ReadString(string section, string ident, string Default)
        {
            return GetVantedValue(section, ident, Default);
        }


        /// <summary>
        /// Write string value
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        public override void WriteString(string section, string ident, string value)
        {
            WriteValue(section, ident, value);
        }
        #endregion


        #region Advanced read/write

        /// <summary>
        /// Read the key names in a section
        /// </summary>
        /// <param name="section"></param>
        /// <returns>string[]</returns>
        public override string[] ReadSectionKeys(string section)
        {
            return ReadSectionNamesAndValues(section).AllKeys;
        }

        /// <summary>
        /// Returns NameValueCollection of key-value pairs found in that section. If section does not exist, a blank NameValueCollection is returned.
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public override NameValueCollection ReadSectionNamesAndValues(string section)
        {
            var keys = new NameValueCollection(10);
            foreach (var sTmp in from string s in GetSection(section) select s.Trim())
            {
                if (sTmp.IndexOf('=') == -1) keys.Add(sTmp, "");
                else
                {
                    var sKeys = sTmp.Split(new[] { '=' }, 2);
                    keys.Add(sKeys[0], sKeys[1]);
                }
            }

            return keys;
        }

        /// <summary>
        /// Read all section names
        /// </summary>
        /// <returns></returns>
        public override string[] ReadSections()
        {
            var arr = new ArrayList(10);
            foreach (var sTmp in ReadFile().Select(s => s.Trim()).Where(sTmp => sTmp.StartsWith("[") && sTmp.EndsWith("]")))
            {
                arr.Add(sTmp);
            }
            return (string[])arr.ToArray(typeof(string));
        }

        /// <summary>
        /// Return all values in string[] array
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public override string[] ReadSectionValues(string section)
        {
            var nv = new NameValueCollection(ReadSectionNamesAndValues(section));
            var arr = new ArrayList(nv.Count);
            for (var i = 0; i < nv.Count; i++)
            {
                arr.Add(nv[i]);
            }
            return (string[])arr.ToArray(typeof(string));
        }


        public override void DeleteKey(string section, string ident)
        {
            ident += "=";
            section = String.Concat("[", section, "]");
            var sectionFound = false;
            foreach (var sTmp in ReadFile().Select(s => s.Trim()))
            {
                //if it isn't section start, look no further
                if ((!sectionFound) && (sTmp.Equals(section)))
                {
                    sectionFound = true;
                    continue;
                }
                if (!sectionFound) continue;

                //does string start with "Ident="
                if (sTmp.StartsWith(ident))
                {
                    break;
                }

                //if we get here, then a new section starts
                if (sTmp.StartsWith("[") && sTmp.EndsWith("]")) break;
            }
        }

        public override void EraseSection(string section)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        #endregion



        #region general helper methods

        /// <summary>
        /// Reads the file to string[]
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> ReadFile()
        {
            return File.Exists(_fileName) ? File.ReadAllLines(_fileName) : new string[0];
        }

        /// <summary>
        /// Write string[] to file, replacing existing contents
        /// </summary>
        /// <param name="lines"></param>
        private void WriteFile(string[] lines)
        {
            try
            {
                File.WriteAllLines(_fileName, lines);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Gives line which is in given section and starts with "key="    
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <returns></returns>
        private string ReadWantedLine(string section, string ident)
        {
            ident += "=";
            var sResult = "";
            section = String.Concat("[", section, "]");
            var sectionFound = false;
            foreach (var sTmp in ReadFile().Select(s => s.Trim()))
            {
                //if it isn't section start, look no further
                if ((!sectionFound) && (sTmp.Equals(section)))
                {
                    sectionFound = true;
                    continue;
                }
                if (!sectionFound) continue;

                //does string start with "Ident="
                if (sTmp.StartsWith(ident))
                {
                    sResult = sTmp;
                    break;
                }

                //if we get here, then a new section starts
                if (sTmp.StartsWith("[") && sTmp.EndsWith("]")) break;
            }
            return sResult;
        }

        private ArrayList GetSection(string section)
        {

            section = String.Concat("[", section, "]");
            var sectionFound = false;
            var arr = new ArrayList();
            foreach (var s in ReadFile())
            {
                var sTmp = s.Trim();

                //if it isn't section start, look no further
                if ((!sectionFound) && (sTmp.Equals(section)))
                {
                    sectionFound = true;
                    continue;
                }
                if (!sectionFound) continue;

                //if we get here, then a new section starts
                if (sTmp.StartsWith("[") && sTmp.EndsWith("]")) break;

                //add to arraylist
                arr.Add(s);
            }

            return arr;
        }

        //TODO: getWantedValue and writeValue could use a whole lot of optimization and changes, especially writeValue!
        /// <summary>
        /// Returns value as string
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        private string GetVantedValue(string section, string ident, string Default)
        {
            try
            {
                var sTmp = ReadWantedLine(section, ident);
                if (string.IsNullOrEmpty(sTmp)) return Default;
                sTmp = sTmp.Substring(sTmp.IndexOf('=') + 1);
                return sTmp.Trim();
            }
            catch { return Default; }
        }

        /// <summary>
        /// Write value to the file, replacing as needed. Needs badly better implementation!
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        private void WriteValue(string section, string ident, string value)
        {
            var identUpdated = ident + "=";
            var sectionUpdated = String.Concat("[", section, "]");

            var sb = new StringBuilder();

            bool sectionExists = false, valueAdded = false, correctSection = false;

            foreach (var s in ReadFile())
            {
                var sTmp = s.Trim();

                //if it isn't section start, look no further
                if ((!sectionExists) && (sTmp.Equals(sectionUpdated)))
                {
                    sb.AppendLine(s);
                    correctSection = true;
                    sectionExists = true;
                    continue;
                }

                //does string start with "Ident="
                if (sTmp.StartsWith(identUpdated) && correctSection)
                {
                    sb.AppendLine(string.Concat(ident, "=", value));
                    valueAdded = true;
                    continue;
                }

                //if we get here, then a new section starts
                if (sTmp.StartsWith("[") && sTmp.EndsWith("]"))
                {
                    if ((correctSection) && (!valueAdded))
                    {
                        sb.AppendLine(string.Concat(ident, "=", value));
                        valueAdded = true;
                    }
                    correctSection = false;
                }
                sb.AppendLine(s);
            }

            //end of readFile array and no value added --> no section or no value
            if (!valueAdded)
            {
                if (!sectionExists) sb.AppendLine(string.Concat("[", section, "]"));
                sb.AppendLine(string.Concat(ident, "=", value));
            }

            var sw = File.CreateText(_fileName);
            sw.Write(sb.ToString());
            sw.Close();
        }

        #endregion


    }
}
