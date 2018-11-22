using System;
using System.Collections.Specialized;
using System.IO;
using System.Globalization;

namespace CheckLogic
{
    public abstract class BaseIni
    {


        protected string _fileName;

        /// <summary>
        /// Ini file to read/write
        /// </summary>
        protected string FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }


        //abstract methods, overridden in all ini versions
        public abstract bool KeyExists(string section, string ident);
        public abstract bool SectionExists(string section);
        public abstract string[] ReadSectionKeys(string section);
        public abstract NameValueCollection ReadSectionNamesAndValues(string section);
        public abstract string[] ReadSections();
        public abstract string[] ReadSectionValues(string section);
        public abstract void DeleteKey(string section, string ident);

        public abstract string ReadString(string section, string ident, string Default);
        public abstract void WriteString(string section, string ident, string value);

        /// <summary>
        /// Clears the ini file. 
        /// </summary>
        public virtual void Clear()
        {
            try
            {
                File.WriteAllText(_fileName, "");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Cannot be implemented in all ini versions, therefore virtual
        /// </summary>
        /// <param name="section"></param>
        public virtual void EraseSection(string section) { throw new NotImplementedException(); }

        /// <summary>
        /// Read integer from ini
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        public virtual int ReadInteger(string section, string ident, int Default)
        {
            try
            {
                return Convert.ToInt32(ReadString(section, ident, Convert.ToString(Default, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
            }
            catch { return Default; }
        }

        /// <summary>
        /// Write integer to ini file
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        public void WriteInteger(string section, string ident, int value)
        {
            try
            {
                WriteString(section, ident, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        /// <summary>
        /// Returns float value
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        public Double ReadFloat(string section, string ident, Double Default)
        {
            try
            {
                var source = ReadString(section, ident, Convert.ToString(Default));
                return FloatParse(source);
            }
            catch { return Default; }
        }

        private static double FloatParse(string value)
        {
            double result;
            var sval = value.Replace(',', '.');
            if (double.TryParse(sval, out result))
                return result;
            sval = value.Replace('.', ',');
            return double.TryParse(sval, out result) ? result : result;
        }

        /// <summary>
        /// Write float (double) to ini file
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        public void WriteFloat(string section, string ident, Double value)
        {
            try
            {
                WriteString(section, ident, Convert.ToString(value));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        /// <summary>
        /// Read DateTime
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        public DateTime ReadDate(string section, string ident, DateTime Default)
        {
            try
            {
                return Convert.ToDateTime(ReadString(section, ident, Convert.ToString(Default)));
            }
            catch { return Default; }
        }

        /// <summary>
        /// Write DateTime
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        public void WriteDate(string section, string ident, DateTime value)
        {
            try
            {
                WriteString(section, ident, Convert.ToString(value));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        /// <summary>
        /// Read boolean value
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        public bool ReadBool(string section, string ident, bool Default)
        {
            try
            {
                var svalue = ReadString(section, ident, Convert.ToString(Default));
                return Convert.ToBoolean(svalue);
            }
            catch { return Default; }
        }

        /// <summary>
        /// Write boolean value
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ident"></param>
        /// <param name="value"></param>
        public void WriteBool(string section, string ident, bool value)
        {
            try
            {
                WriteString(section, ident, Convert.ToString(value));
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

    }
}
