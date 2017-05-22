using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Text;
//using Microsoft.SqlServer.ConnectionInfo;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Sdk.Sfc;
//using Microsoft.SqlServer.Smo;
using System.IO;

namespace TestCS
{
        
    public class TemplateHelper
    {

        //List<SqlSmoObject> listSP;
        List<StoredProcedure> listSP;
        
        private string Key(StoredProcedure sp)
        {
            switch (sp.CrudType())
            {
                case ICCrudType.Read:
                    return sp.Name.Substring(2, sp.Name.Length-2) + "_Select";
                case ICCrudType.Create:
                    return sp.Name.Substring(2, sp.Name.Length - 2) + "_Insert";
                case ICCrudType.Update:
                    return sp.Name.Substring(2, sp.Name.Length - 2) + "_Update";
                case ICCrudType.Delete:
                    return sp.Name.Substring(2, sp.Name.Length - 2) + "_Delete";
                case ICCrudType.Custom:
                    return sp.Name + "_Custom";

                //case ICCrudType.Search:
                //    return sp.Name.Substring(2, sp.Name.Length-2) + "_Search";
                default:
                    return "zzIgnored_"+sp.Name;
            }
        }


        public TemplateHelper(string ConnectionString, bool webcalls = false)
        {
            var server = new Server(new ServerConnection(new SqlConnection(ConnectionString)));
            var connectionStringBuilder = new SqlConnectionStringBuilder(ConnectionString);
            var db = new Database(server, connectionStringBuilder.InitialCatalog);

            listSP = new List<StoredProcedure>();
            DataTable dataTable = db.EnumObjects(DatabaseObjectTypes.StoredProcedure);
            foreach (DataRow row in dataTable.Rows)
            {
                string sSchema = (string)row["Schema"];
                if (sSchema == "tmp" || sSchema == "old" || sSchema == "sys" || sSchema == "INFORMATION_SCHEMA")
                    continue;
                var sp = (StoredProcedure)server.GetSmoObject(
                    new Urn((string)row["Urn"]));
                if (!sp.IsSystemObject) 
                    listSP.Add(sp);
            }


            if (webcalls)
            {
                WriteLine(
                    @"Option Explicit On 
Option Strict On
Imports System
Imports System.Data
Imports System.Data.SqlClient


Namespace CAST.InfoCastServer.eSupportPortal.DataAccess

    <System.CodeDom.Compiler.GeneratedCodeAttribute(""CAST Generation tool"", ""0.1"")> _ 
    Public Partial Class DatabaseSQL
");
            }
            else
            {
                

            WriteLine(@"Option Explicit On 
Option Strict On

Imports System
Imports System.Data
Imports System.Data.Common
Imports System.Data.SqlClient
Imports CAST.InfoCAST.Common
Imports CAST.InfoCAST.Common.Functions
Imports CAST.InfoCAST.Common.GlobalEnumeration
Imports CAST.InfoCAST.DataAccess


Namespace CAST.InfoCAST.DataAccess

    <System.CodeDom.Compiler.GeneratedCodeAttribute(""CAST Generation tool"", ""0.1"")> _ 
    Public Partial Class DatabaseSQL
");
 }

            int i = 0;
            listSP.Sort(delegate(StoredProcedure sp, StoredProcedure sp2)
                {
                    return (Key(sp).ToUpper().CompareTo(Key(sp2).ToUpper()));
                });
            foreach(StoredProcedure sp in listSP)
            {
                //if (i > 30) break;
                /* 
                 * if (sp.Name != "U_Address")
                    continue;
                 */
                if (webcalls)
                {

                    if (sp.Name.ToLower().StartsWith("webcalls_") ||
                        sp.Name.ToLower().StartsWith("webcall_")) 
                    {
                        WriteWrapper(sp);
                    }

                }
                else
                {
                    //exclude dummy stored procedures
                    if (sp.Name.ToLower().StartsWith("ztodelete_")
                        || sp.Name.ToLower().StartsWith("z201")
                        || sp.Name.ToLower().StartsWith("webcalls_")
                        || sp.Name.ToLower().StartsWith("webcall_")
                        || sp.Name.ToLower().StartsWith("web_")
                        || sp.Name.ToLower().StartsWith("admin_")
                        || sp.Name.ToLower().StartsWith("report")
                        || sp.Name.ToLower().StartsWith("job_")
                        || sp.Name.ToLower().StartsWith("hubspot_")
                        || sp.Name.ToLower().StartsWith("icm_")
                        || sp.Name.ToLower().EndsWith("gbe")
                        || sp.Name.ToLower().EndsWith("cpo")
                        || sp.Name.ToLower().EndsWith("rmi")
                        || sp.Name.ToLower().EndsWith("_tbd") //To be deleted
                        )
                    {
                        //Skip
                    }
                    else if ((sp.Name.ToLower().Contains("_param") //IC-731
                            && !sp.Name.ToLower().Contains("cle")))
                    {
                        //Skip deprecated
                    }
                    else //if (sp.Name == "I_BusinessLine")
                    {

                        WriteWrapper(sp);
                    }
                }
                i++;
            }

            WriteLine(@"    End Class

End Namespace");

        }


        public void WriteWrapper(StoredProcedure sp)
        {

            int parameterIndex = 0;
            // 
            string d1 = @"        Function Exec_{0} (";
            string d2 = @"        Function Exec_D{0} (";
            switch (sp.CrudType()) {
                case ICCrudType.Read:
                //case ICCrudType.Search:
                    ActivateBuffer();
                    Write1(d1, sp.Name);
                    Write2(d2, sp.Name);
                    Write1("ByRef DS as DataSet");
                    parameterIndex++;
                    break;
                default:
                    Write1(d1, sp.Name);
                    break;
            }


            foreach (StoredProcedureParameter spp in sp.ParametersBeforeDebug())
            {
                if (parameterIndex > 0)
                {
                    if (parameterIndex == 1 && sp.CrudType() == ICCrudType.Read) {
                        Write1(", ");
                    } else {
                        Write(", ");
                    }

                    if ((parameterIndex % 4) == 0) {//4 params per line
                        WriteLine("_");
                        Write("                ");
                    }
                }
                if (spp.DataType.GetCLRType() == "Byte[]") //Special handling in VB for Array!
                    Write("{2} v{0}() As Byte", spp.NameWithoutAt(), spp.DataType.GetCLRType(), spp.IsOutputParameter ? "ByRef" : "ByVal");
                else if (spp.Name.ToLower() == "@debug" && spp.DataType.GetCLRType() == "Boolean" && spp.DefaultValue == "0")
                    Write("Optional ByVal vDebug As Boolean = False");
                else if (spp.IsNullableParameter() && !spp.IsByRef() ) //&& !spp.IsNullableCRLType()) //.GetCLRType() != "String" && spp.DataType.GetCLRType() != "Date?" && spp.DataType.GetCLRType() != "DateTime?")
                    Write("{2} v{0} As Nullable(Of {1})", spp.NameWithoutAt(), spp.DataType.GetCLRType(), spp.IsOutputParameter ? "ByRef" : "ByVal");
                else if (spp.DataType.Name == "T_Identifiant" && !spp.IsOutputParameter) //Replace -1 by Nothing
                    Write("{2} v{0} As Nullable(Of {1})", spp.NameWithoutAt(), spp.DataType.GetCLRType(), spp.IsOutputParameter ? "ByRef" : "ByVal");
                else // ByRef
                    Write("{2} v{0} As {1}", spp.NameWithoutAt(), spp.DataType.GetCLRType(), spp.IsOutputParameter ? "ByRef" : "ByVal");

                parameterIndex++;
            }

            switch (sp.CrudType())
            {
                case ICCrudType.Read:
                    WriteLine1(@") As Long", sp.Name);
                    WriteLine2(@") As DataSet", sp.Name);
                    WriteLine2(@"            Dim DS As New DataSet");
                    break;
                case ICCrudType.Create:
                case ICCrudType.Update:
                case ICCrudType.Delete:
                case ICCrudType.Custom:
                //case ICCrudType.Search:
                    WriteLine(@") As Long", sp.Name);
                    break;
                default:
                    WriteLine(@") As Object", sp.Name);
                    break;
            }

            parameterIndex = 0;
            foreach (StoredProcedureParameter spp in sp.ParametersBeforeDebug())
            {
                string c1 =          @"            Dim param{0} As New SqlParameter(""{1}"", {2})";
                string c2 =          @"            param{0}.Value = v{1}";
                string c2null =      @"            param{0}.Value = IIf(v{1} Is Nothing, DBNull.Value, v{1})";
                string c2string =    @"            param{0}.Value = IIf(v{1} Is Nothing, DBNull.Value, v{1}.CheckLength({2},"""+sp.Name+@".{1}""))";
                string c2nullable  = @"            param{0}.Value = IIf(Not v{1}.HasValue, DBNull.Value, v{1})";
                string c2datetime_nullabledatetime = @"            param{0}.Value = IIf(Not v{1}.IsValidSQLDate(""" + sp.Name + @".{1}""), DBNull.Value, v{1})";
                string c2T_Id      = @"            param{0}.Value = IIf(Not v{1}.IsValidID(""" + sp.Name + @".{1}""), DBNull.Value, v{1})";
//                string c2nullabledatetime = @"            param{0}.Value = IIf(Not v{1}.IsValidSQLDate("""+sp.Name+@".{1}""), DBNull.Value, v{1})";

                WriteLine(c1, spp.NameWithoutAt(), spp.Name /* with @*/, spp.DataType.GetDataTypeDef());
                if (spp.IsOutputParameter)
                {
                    WriteLine(@"            param{0}.Direction = ParameterDirection.Output", spp.NameWithoutAt());
                }
                else
                {
                    if (spp.DataType.GetCLRType() == "String" && spp.DataType.MaximumLength > 0 
                            && spp.DataType.SqlDataType != SqlDataType.Text 
                            && spp.DataType.SqlDataType != SqlDataType.NText) //Types with Nothing
                        //For varchar(max), MaximumLength = -1
                        WriteLine(c2string, spp.NameWithoutAt(), spp.NameWithoutAt(), spp.DataType.MaximumLength);
                    else if ((spp.IsNullableParameter() && (spp.IsNullableCRLType() || !spp.IsByRef()))
                                 || (spp.DataType.Name == "T_Identifiant" && !spp.IsOutputParameter) 
                                 || (spp.DataType.Name == "T_NDate") )

                        if (spp.DataType.Name == "T_Identifiant") 
                        {
                            WriteLine(c2T_Id, spp.NameWithoutAt(), spp.NameWithoutAt());
                        } else if (spp.DataType.GetCLRType() == "DateTime?" || spp.DataType.GetCLRType() == "Nulllable(Of DateTime)") 
                        {
                            WriteLine(c2datetime_nullabledatetime, spp.NameWithoutAt(), spp.NameWithoutAt());
                        }
                        else 
                        {
                            WriteLine(c2nullable, spp.NameWithoutAt(), spp.NameWithoutAt());
                        }

                    else if (spp.DataType.GetCLRType()  == "String" || 
                            spp.IsNullableParameter()  ) //Types with Nothing
                        WriteLine(c2null, spp.NameWithoutAt(), spp.NameWithoutAt());
                    else if (spp.DataType.GetCLRType() == "DateTime")
                        WriteLine(c2datetime_nullabledatetime, spp.NameWithoutAt(), spp.NameWithoutAt());
                    else
                        WriteLine(c2, spp.NameWithoutAt(), spp.NameWithoutAt());
                }
                parameterIndex ++;
            }
            WriteLine("");

            switch (sp.CrudType())
            {
                case ICCrudType.Read:
                //case ICCrudType.Search:
                    Write(@"           Dim lngRtrn As Long = ExecuteSelectWithSP(""{0}"", DS", sp.Name);
                    parameterIndex = 0;
                    foreach (StoredProcedureParameter spp in sp.ParametersBeforeDebug())
                    {
                        if (parameterIndex > 0 & (parameterIndex % 4) == 0)
                        {
                            WriteLine(" _");
                            Write("                ");
                        } 
                        Write(", param" + spp.NameWithoutAt());
                        parameterIndex ++;
                    }
                    WriteLine(" )");
                    break;
                case ICCrudType.Create:
                case ICCrudType.Update:
                case ICCrudType.Delete:
                case ICCrudType.Custom:

                    Write(@"            Dim lngRtrn As Long = ExecuteStoredProc(""{0}""", sp.Name);
                    parameterIndex = 0;
                    foreach (StoredProcedureParameter spp in sp.ParametersBeforeDebug())
                    {
                        if (parameterIndex> 0 & (parameterIndex % 4) == 0)
                        {
                            WriteLine(" _");
                            Write("                ");
                        }
                        Write(", param" + spp.NameWithoutAt());
                        parameterIndex ++;
                    }
                    WriteLine(" )");
                    break;
                default:
                    WriteLine("                Throw new NotImplementedException()");
                    break;
            }

            parameterIndex = 0;
            foreach (StoredProcedureParameter spp in sp.ParametersBeforeDebug())
            {
                if (spp.IsOutputParameter)
                {
                    WriteLine(@"                v{1} = CType(param{0}.Value, {2})",
                            spp.NameWithoutAt(), spp.NameWithoutAt(), spp.DataType.GetCLRType());
                }
            }
            switch (sp.CrudType())
            {
                case ICCrudType.Read:
                    WriteLine1(@"                Return lngRtrn");
                    WriteLine2(@"                Return DS");
                    break;
                case ICCrudType.Create:
                case ICCrudType.Update:
                case ICCrudType.Delete:
                case ICCrudType.Custom:
                //case ICCrudType.Search:
                    WriteLine(@"                Return lngRtrn");
                    break;
                default:
                    break;
            }

            WriteLine(@"            End Function");
            FlushBuffer();

        }



        /// <summary>
        /// Writes stored procedure parameter declarations for all columns of the 
        /// specified table. For IDENTITY and TIMESTAMP columns parameters are 
        /// generated as OUTPUT in the end of parameter list.
        /// </summary>
        public void WriteParameterDeclarations(Table table)
        {
            PushIndent("    ");

            Column identityColumn = null;
            Column timestampColumn = null;
            int parameterIndex = 0;
            for (int i = 0; i < table.Columns.Count; i++)
            {
                Column column = table.Columns[i];
                if (column.Identity == true)
                {
                    identityColumn = column;
                    continue;
                }

                if (column.DataType.SqlDataType == SqlDataType.Timestamp)
                {
                    timestampColumn = column;
                    continue;
                }

                // Write input parameter for a regular column
                if (parameterIndex > 0)
                    WriteLine(",");
                Write("@{0} {1}", column.Name, column.DataType.GetDataTypeDeclaration());
                parameterIndex++;
            }

            // Write output parameter for identity column
            if (identityColumn != null)
            {
                if (parameterIndex > 0)
                    WriteLine(",");
                Write("@{0} {1} output", identityColumn.Name, identityColumn.DataType.GetDataTypeDeclaration());
                parameterIndex++;
            }

            // Write output parameter for timestamp column
            if (timestampColumn != null)
            {
                if (parameterIndex > 0)
                    WriteLine(",");
                Write("@{0} {1} output", timestampColumn.Name, timestampColumn.DataType.GetDataTypeDeclaration());
                parameterIndex++;
            }

            PopIndent();
        }

        /// <summary>
        /// Writes list of column names for the INSERT statement
        /// </summary>
        public void WriteInsertClause(Table table)
        {
            PushIndent("        ");
            int columnIndex = 0;
            for (int i = 0; i < table.Columns.Count; i++)
            {
                Column column = table.Columns[i];
                if (column.Identity == true)
                    continue;
                if (column.DataType.SqlDataType == SqlDataType.Timestamp)
                    continue;

                if (columnIndex > 0)
                    WriteLine(",");
                Write("[{0}]", column.Name);
                columnIndex++;
            }
            PopIndent();
        }

        /// <summary>
        /// Writes list of parameter names for VALUES clause of the INSERT statement
        /// </summary>
        public void WriteValuesClause(Table table)
        {
            PushIndent("        ");
            int parameterIndex = 0;
            for (int i = 0; i < table.Columns.Count; i++)
            {
                Column column = table.Columns[i];
                if (column.Identity == true)
                    continue;
                if (column.DataType.SqlDataType == SqlDataType.Timestamp)
                    continue;

                if (parameterIndex > 0)
                    WriteLine(",");
                Write("@{0}", column.Name);
                parameterIndex++;
            }
            PopIndent();
        }



        //void Write(string s) { Console.Write(s); }
        StringBuilder sb; //= new StringBuilder();
        void Write(string s, params object[] p) { 
            Write1(s, p);
            if (sb != null) Write2(s, p);
        }
        //void WriteLine(string s) { Console.WriteLine(s); }
        void WriteLine(string s, params object[] p) {
            WriteLine1(s, p);
            if (sb != null) WriteLine2(s, p);
        }
        void Write1(string s, params object[] p) {  Console.Write(s, p); }
        void WriteLine1(string s, params object[] p) { Console.WriteLine(s, p); }

        void Write2(string s, params object[] p) { sb.AppendFormat(s, p); }
        void WriteLine2(string s, params object[] p) { sb.AppendFormat(s, p).AppendLine(); }

        void FlushBuffer() {
            if (sb != null) {
                Console.WriteLine(sb.ToString());
                sb = null;
            }
        }
        void ActivateBuffer() {
            sb = new StringBuilder();
        }
        void PushIndent(string s) { }
        void PopIndent() { }
    }

        public enum ICCrudType
        {
            Unknown,
            Create,
            Read,
            Update,
            Delete,
            Custom,
            //Search,
        }

    public static class MyExtensions
    {
        public static string NameWithoutAt(this StoredProcedureParameter spp)
        {
            return spp.Name.Substring(1, spp.Name.Length - 1);
        }
        public static bool IsNullableParameter(this StoredProcedureParameter spp)
        {
            return spp.DefaultValue.ToLower() == "null";
        }
        public static bool IsNullableCRLType(this StoredProcedureParameter spp) {
            string crltype = spp.DataType.GetCLRType();
            if (crltype.Contains("?") || crltype.Contains("Nullable(Of")) 
                return true;
            else 
                return false;
        }
        //private static Type CRLType(this StoredProcedureParameter spp) {
        //    return Type.GetType("System." + spp.DataType.GetCLRType().Replace("Long", "Int64"));
        //}
        public static bool IsByRef(this StoredProcedureParameter spp) {
            switch (spp.DataType.GetCLRType()) {
                case "Boolean":
                case "Decimal":
                case "Double":
                case "Byte":
                case "Int16":
                case "Int32":
                case "Int64":
                case "DateTime":
                case "TimeSpan":
                case "Long":
                case "Int":
                    return false;
                case "Date?":
                case "Byte[]":
                case "String":
                default:
                    return true;
            }
        }

        /// <summary>
        /// Identify the CRUD type, based on the stored proc name
        /// </summary>
        /// <param name="sp"></param>
        /// <returns></returns>
        public static ICCrudType CrudType(this StoredProcedure sp)
        {
            switch (sp.Name.Substring(0, 2).ToUpper())
            {
                case "S_":
                case "SU": //SU_InfoCastUser
                    return ICCrudType.Read;
                case "I_":
                case "IU": //IU_DealPartner, ...
                    return ICCrudType.Create;
                case "U_":
                    return ICCrudType.Update;
                case "D_":
                    return ICCrudType.Delete;
            }
            if (sp.Name.StartsWith("GenerateKey")) {
                return ICCrudType.Custom;
            }

            if (sp.Name.ToUpper().StartsWith("WEBCALL_S_") ||
                sp.Name.ToUpper().StartsWith("WEBCALLS_S_")) {
                    return ICCrudType.Read;
                }
            else if (sp.Name.ToUpper().StartsWith("WEBCALL_I_") ||
                sp.Name.ToUpper().StartsWith("WEBCALLS_I_"))
            {
                return ICCrudType.Create;
            }
            else if (sp.Name.ToUpper().StartsWith("WEBCALL_U_") ||
                sp.Name.ToUpper().StartsWith("WEBCALLS_U_"))
            {
                return ICCrudType.Update;
            }
            else if (sp.Name.ToUpper().StartsWith("WEBCALL_D_") ||
                sp.Name.ToUpper().StartsWith("WEBCALLS_D_"))
            {
                return ICCrudType.Delete;
            }

            return ICCrudType.Unknown;
        }


        /// <summary>
        /// Returns a string that contains T-SQL declaration for the specified data 
        /// type. For string data types this includes maximum length, for numeric 
        /// data types this includes scale and precision.
        /// </summary>
        public static string GetDataTypeDeclaration(this DataType dataType) {
            string result = dataType.Name;
            switch (dataType.SqlDataType) {
                case SqlDataType.Binary:
                case SqlDataType.Char:
                case SqlDataType.NChar:
                case SqlDataType.NVarChar:
                case SqlDataType.VarBinary:
                case SqlDataType.VarChar:
                    result += string.Format("({0})", dataType.MaximumLength);
                    break;

                case SqlDataType.NVarCharMax:
                case SqlDataType.VarBinaryMax:
                case SqlDataType.VarCharMax:
                    result += "(max)";
                    break;

                case SqlDataType.Decimal:
                case SqlDataType.Numeric:
                    result += string.Format("({0}, {1})", dataType.NumericPrecision, dataType.NumericScale);
                    break;
            }
            return result;
        }
        //see: http://msdn.microsoft.com/en-us/library/bb386947.aspx#DefaultTypeMapping
        public static string GetCLRType(this DataType dataType) {
            string result = dataType.Name;
            switch (dataType.SqlDataType) {
                case SqlDataType.Binary:
                case SqlDataType.Image:
                    return "Byte[]";

                case SqlDataType.NText:
                case SqlDataType.Text:
                case SqlDataType.Char:
                case SqlDataType.NChar:
                case SqlDataType.NVarChar:
                case SqlDataType.VarBinary:
                case SqlDataType.VarChar:
                case SqlDataType.NVarCharMax:
                case SqlDataType.VarBinaryMax:
                case SqlDataType.VarCharMax:
                    return "String";
                case SqlDataType.Bit:
                    return "Boolean";
                case SqlDataType.Numeric:
                case SqlDataType.Decimal:
                case SqlDataType.Money:
                case SqlDataType.SmallMoney:
                    return "Decimal";
                case SqlDataType.Float:
                    return "Double";
                case SqlDataType.TinyInt:
                    return "Byte";
                case SqlDataType.SmallInt:
                    return "Int16";
                case SqlDataType.Int:
                    return "Int32";
                case SqlDataType.BigInt:
                    return "Int64";
                case SqlDataType.Date:
                case SqlDataType.DateTime:
                case SqlDataType.SmallDateTime:
                case SqlDataType.DateTime2:
                    return "DateTime";
                case SqlDataType.Time:
                    return "TimeSpan";
                case SqlDataType.UserDefinedDataType:
                    switch (dataType.Name) {
                        case "T_Identifiant":
                            return "Long";
                        case "T_NDate":
                            return "Date?";
                        case "T_Amount":
                            return "Int";
                        case "T_Denomination":
                            return "String";
                        case "T_UserId":
                            return "String";
                        default:
                            throw new NotImplementedException("UsedDefinedType: " + dataType.Name);
                    }
                default:
                    throw new NotImplementedException("Type: " + dataType.SqlDataType);


            }
        }
        public static string GetDataTypeDef(this DataType dataType) {
            //http://msdn.microsoft.com/en-us/library/bb386947.aspx#DefaultTypeMapping
            string result = "SqlDbType." + dataType.SqlDataType.ToString();
            switch (dataType.SqlDataType) {
                case SqlDataType.Binary:
                case SqlDataType.Char:
                case SqlDataType.NChar:
                case SqlDataType.NVarChar:
                case SqlDataType.VarBinary:
                case SqlDataType.VarChar:
                    result += string.Format(", {0}", dataType.MaximumLength);
                    break;

                case SqlDataType.Decimal:
                case SqlDataType.Numeric:
                    if (dataType.NumericScale == 0) {
                        return "SqlDbType.Int";
                    }
                    return "SqlDbType.Decimal";
                case SqlDataType.Float:
                    return string.Format("SqlDbType.Float, {0}", dataType.NumericPrecision);
                case SqlDataType.UserDefinedDataType:
                    switch (dataType.Name) {
                        case "T_Identifiant":
                            return "SqlDbType.Int";
                        case "T_NDate":
                            return "SqlDbType.DateTime";
                        case "T_Amount":
                            return "SqlDbType.Float, 9";
                        case "T_Denomination":
                            return "SqlDbType.Char, 4";
                        case "T_UserId":
                            return $"SqlDbType.VarChar, {dataType.MaximumLength}";
                        default:
                            throw new NotImplementedException("UsedDefinedType: " + dataType.Name);
                    }
                case SqlDataType.NVarCharMax:
                    return "SqlDbType.Char";
                case SqlDataType.VarBinaryMax:
                    return "SqlDbType.Binary";
                case SqlDataType.VarCharMax:
                    return "SqlDbType.VarChar";
                default:
                    result += "";
                    break;
            }
            return result;
        }

        /// <summary>
        /// We want to retrieve the parameters that are before @debug BIT = 0
        /// this parameter indicates that the following parameters are obsolete and should not be generated.
        /// </summary>
        /// <param name="sp"></param>
        /// <returns></returns>
        public static Collection<StoredProcedureParameter> ParametersBeforeDebug(this StoredProcedure sp)
        {
            var coll = new Collection<StoredProcedureParameter>();
            var debugFound = false;
            foreach (StoredProcedureParameter spp in sp.Parameters)
            {
                 if (String.Compare("@Debug", spp.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
                 {
                     debugFound = true;
                 }
                if (!debugFound)
                {
                    coll.Add(spp);
                }
            }
            return coll;
        }
    }
}
