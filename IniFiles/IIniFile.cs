using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;

namespace CheckLogic
{
    /// <summary>
    /// Interface to simplify implementation of ini handlers. Not actually used except for helping to generate method stubs.
    /// </summary>
    interface IIniFile
    {

        string FileName { get; set; }

        bool KeyExists(string Section, string Ident);
        bool SectionExists(string Section);

        string[] ReadSectionKeys(string Section);
        NameValueCollection ReadSectionNamesAndValues(string Section);
        string[] ReadSections();
        string[] ReadSectionValues(string Section);
        void DeleteKey(string Section, string Ident);
        void EraseSection(string Section);
        void Clear();

        #region Basic read and write
        string ReadString(string Section, string Ident, string Default);
        bool ReadBool(string Section, string Ident, bool Default);
        DateTime ReadDate(string Section, string Ident, DateTime Default);
        double ReadFloat(string Section, string Ident, double Default);
        int ReadInteger(string Section, string Ident, int Default);
        void WriteBool(string Section, string Ident, bool Value);
        void WriteDate(string Section, string Ident, DateTime Value);
        void WriteFloat(string Section, string Ident, double Value);
        void WriteInteger(string Section, string Ident, int Value);
        void WriteString(string Section, string Ident, string Value);
        #endregion
    }
}
