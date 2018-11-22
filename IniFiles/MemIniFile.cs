using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Collections;
using System.Linq;
using System.Threading;

namespace CheckLogic
{
    /// <summary>
    /// Inifile cached in memory, offers better performance then IniFile. Original file is not modified until UpdateFile() is called.
    /// </summary>
    public class MemIniFile : BaseIni
    {
        /// <summary>
        /// Private, contains [name]:NameValueCollection values
        /// </summary>
        private readonly HybridDictionary _values = new HybridDictionary(10, false);


        #region Load and Save;
        /// <summary>
        /// Create the class, load the ini file to memory
        /// </summary>
        /// <param name="fileName">File to load</param>    
        public MemIniFile(string fileName)
        {
            FileName = fileName;
            //Load
            LoadIni(this._fileName);

        }

        /// <summary>
        /// Reloads ini from the file without updating it.
        /// </summary>
        public void ReLoad()
        {
            _values.Clear();
            LoadIni(_fileName);
        }

        /// <summary>
        /// Load ini to memory. Private
        /// </summary>
        /// <param name="name"></param>
        private void LoadIni(string name)
        {
            var trycount = 10;
            while (true)
            {
                try
                {
                    _values.Clear();
                    //no file found, nothing to load
                    if (!File.Exists(name)) return;
                    //read file to string array
                    var allLines = File.ReadAllLines(name);

                    #region Local variables

                    var sHeader = "";
                    var keys = new NameValueCollection();

                    #endregion


                    //and restructure it in memory for faster access
                    foreach (var sTmp in allLines.Select(s => s.Trim()).Where(sTmp =>
                                                                              !string.IsNullOrEmpty(sTmp))
                                                 .Where(sTmp => !sTmp.StartsWith(";")))
                    {
                        //[header]
                        if (sTmp.StartsWith("[") && sTmp.EndsWith("]"))
                        {
                            //add values to collection
                            if (sHeader != "") _values.Add(sHeader, new NameValueCollection(keys));
                            //new header
                            sHeader = sTmp.Substring(1, sTmp.Length - 2);
                            keys.Clear();
                            continue;
                        }
                        if (sTmp.IndexOf('=') == -1) keys.Add(sTmp, "");
                        else
                        {
                            var sKeys = sTmp.Split(new[] {'='}, 2);
                            keys.Add(sKeys[0], sKeys[1]);
                        }
                    }
                    for (var i = 0; i < keys.Count; i++)
                    {
                        WriteString(sHeader, keys.GetKey(i), keys.Get(i));
                    }
                    break;
                }
                catch (ArgumentException arex)
                {
                    Console.WriteLine(arex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    trycount--;
                    if (trycount <= 0)
                        throw new Exception(ex.Message);
                    Thread.Sleep(500);
                }
            }
        }

        public void FromString(string value)
        {
            try
            {
                _values.Clear();

                //read file to string array
                var allLines = value.Split(new[] { '\n' });

                #region Local variables
                var sHeader = "";
                var keys = new NameValueCollection();
                #endregion

                //and restructure it in memory for faster access
                foreach (var sTmp in allLines.Select(s => s.Trim()).Where(sTmp =>
                    !string.IsNullOrEmpty(sTmp)).Where(sTmp => !sTmp.StartsWith(";")))
                {
                    //[header]
                    if (sTmp.StartsWith("[") && sTmp.EndsWith("]"))
                    {
                        //add values to collection
                        if (sHeader != "") _values.Add(sHeader, new NameValueCollection(keys));
                        //new header
                        sHeader = sTmp.Substring(1, sTmp.Length - 2);
                        keys.Clear();
                        continue;
                    }
                    if (sTmp.IndexOf('=') == -1) keys.Add(sTmp, "");
                    else
                    {
                        var sKeys = sTmp.Split(new[] { '=' }, 2);
                        keys.Add(sKeys[0], sKeys[1]);
                    }
                }
                for (var i = 0; i < keys.Count; i++)
                {
                    WriteString(sHeader, keys.GetKey(i), keys.Get(i));
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public override string ToString()
        {
            try
            {
                var list = new List<string>();
                foreach (string section in _values.Keys)
                {
                    list.Add(string.Concat("[", section, "]"));
                    var nv = (NameValueCollection)_values[section];
                    list.AddRange(from string key in nv.Keys select key + "=" + nv[key]);
                }
                return String.Join("\n", list.ToArray());

            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        /// <summary>
        /// Save/update the ini file
        /// </summary>   
        public void UpdateFile()
        {
            var trycount = 10;
            while (true)
            {
                try
                {
                    File.Delete(_fileName);
                    using (var sw = File.CreateText(_fileName))
                    {
                        foreach (string section in _values.Keys)
                        {
                            sw.WriteLine(string.Concat("[", section, "]"));
                            var nv = (NameValueCollection) _values[section];
                            foreach (string key in nv.Keys)
                            {
                                sw.WriteLine(key + "=" + nv[key]);
                            }
                            sw.WriteLine(); //empty line after section name
                        }
                    }
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                    trycount--;
                    if (trycount <= 0) break;
                }
            }
        }

        /// <summary>
        /// Clears the ini information from memory, does not delete the contents of the ini file - call UpdateFile() after Clear() if you want to do that.
        /// </summary>
        public override void Clear()
        {
            _values.Clear();
        }

        #endregion

        #region Read and write basic values
        /// <summary>
        /// Read string from ini
        /// </summary>
        /// <param name="section">Section name without []</param>
        /// <param name="ident">Key</param>
        /// <param name="Default">Default value to return in case the value is missing</param>
        /// <returns>String</returns>
        public override string ReadString(string section, string ident, string Default)
        {
            if (!_values.Contains(section)) return Default;
            var nv = (NameValueCollection)_values[section];
            var s = nv.Get(ident);
            return s ?? Default;
        }

        /// <summary>
        /// Write string to ini
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        public override void WriteString(string section, string ident, string value)
        {
            NameValueCollection nv;
            if (!_values.Contains(section))
            {
                nv = new NameValueCollection();
                nv[ident] = value;
                _values.Add(section, nv);
            }
            else
            {
                nv = (NameValueCollection)_values[section];
                nv[ident] = value;
            }
        }



        #endregion

        #region Advanced read/manipulate

        /// <summary>
        /// Returns NameValueCollection of key-value pairs found in that section. If section does not exist, a blank NameValueCollection is returned.
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public override NameValueCollection ReadSectionNamesAndValues(string section)
        {
            return !_values.Contains(section) ? new NameValueCollection() : new NameValueCollection((NameValueCollection)_values[section]);
        }

        /// <summary>
        /// Delete section from the ini.
        /// </summary>
        /// <param name="section"></param>
        public override void EraseSection(string section)
        {
            _values.Remove(section);
        }

        /// <summary>
        /// Read the key names in a section
        /// </summary>
        /// <param name="section"></param>
        /// <returns>string[]</returns>
        public override string[] ReadSectionKeys(string section)
        {
            return !_values.Contains(section) ? new string[0] : ((NameValueCollection)_values[section]).AllKeys;
        }

        /// <summary>
        /// Return all values in string[] array
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public override string[] ReadSectionValues(string section)
        {
            if (!_values.Contains(section))
            {
                return new string[0];
            }
            var arr = new ArrayList();
            var nv = new NameValueCollection((NameValueCollection)_values[section]);
            foreach (string s in (nv.Keys))
            {
                arr.Add(nv[s]);
            }
            return (string[])arr.ToArray(typeof(string));
        }

        /// <summary>
        /// Read all section names
        /// </summary>
        /// <returns></returns>
        public override string[] ReadSections()
        {            
            var result = new string[_values.Keys.Count];
            var i = 0;
            foreach(string item in _values.Keys)
            {
                result[i++] = item;
            }
            return result;
        }

        /// <summary>
        /// Deletes key from section
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        public override void DeleteKey(string section, string ident)
        {
            if (!_values.Contains(section)) return;

            ((NameValueCollection)_values[section]).Remove(ident);
        }

        /// <summary>
        /// Check if a section exists
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public override bool SectionExists(string section)
        {
            return _values.Contains(section);
        }

        /// <summary>
        /// Check if a key exists in specified section
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <returns></returns>
        public override bool KeyExists(string section, string ident)
        {
            if (!_values.Contains(section)) return false;
            return (((NameValueCollection)_values[section]).Get(ident) != null);
        }

        #endregion

    }
}
