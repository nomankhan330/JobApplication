using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Helper
{
    public static class DataHelper
    {
        public static readonly string _DefaultDateFormat = "dd-MMM-yyyy";

        public static string stringParse(object value)
        {
            try
            {
                if (value != null)
                {
                    return value.ToString();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static string stringParse(object value, string defaultValue)
        {
            try
            {
                if (value != null)
                {
                    return value.ToString();
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static int intParse(object value)
        {
            try
            {
                if (value != null)
                {
                    int i = 0;
                    string[] values = value.ToString().Split('.');


                    int.TryParse(replace_values(values[0]), out i);
                    return i;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int intParse(object value, int defaultValue)
        {
            try
            {
                if (value != null)
                {
                    int i = 0;
                    string[] values = value.ToString().Split('.');


                    if (int.TryParse(replace_values(values[0]), out i))
                        return i;
                    else
                        return defaultValue;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static long longParse(object value)
        {
            try
            {
                if (value != null)
                {
                    long i = 0;
                    long.TryParse(replace_values(value), out i);
                    return i;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static long longParse(object value, long defaultValue)
        {
            try
            {
                if (value != null)
                {
                    long i = 0;
                    if (long.TryParse(replace_values(value), out i))
                        return i;
                    else
                        return defaultValue;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static bool boolParse(object value)
        {
            try
            {
                if (value != null)
                {
                    bool i = false;

                    if (value.ToString().ToLower() == "on")
                        return true;

                    bool.TryParse(replace_values(value), out i);
                    return i;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool boolParse(object value, bool defaultValue)
        {
            try
            {
                if (value != null)
                {
                    bool i = false;

                    if (value.ToString().ToLower() == "on")
                        return true;

                    if (bool.TryParse(replace_values(value), out i))
                        return i;
                    else
                        return defaultValue;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static double doubleParse(object value)
        {
            try
            {
                if (value != null)
                {
                    double i = 0;
                    double.TryParse(replace_values(value), out i);
                    return i;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static double doubleParse(object value, double defaultValue)
        {
            try
            {
                if (value != null)
                {
                    double i = 0;
                    if (double.TryParse(replace_values(value), out i))
                        return i;
                    else
                        return defaultValue;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static float floatParse(object value)
        {
            try
            {
                if (value != null)
                {
                    float i = 0;
                    float.TryParse(replace_values(value), out i);
                    return i;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static float floatParse(object value, float defaultValue)
        {
            try
            {
                if (value != null)
                {
                    float i = 0;
                    if (float.TryParse(replace_values(value), out i))
                        return i;
                    else
                        return defaultValue;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static decimal decimalParse(object value)
        {
            try
            {
                if (value != null)
                {
                    decimal i = 0;
                    decimal.TryParse(replace_values(value), out i);
                    return i;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static decimal decimalParse(object value, decimal defaultValue)
        {
            try
            {
                if (value != null)
                {
                    decimal i = defaultValue;
                    decimal.TryParse(replace_values(value), out i);
                    return i;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static DateTime dateParse(object value)
        {
            try
            {
                if (value != null)
                {
                    DateTime i;
                    if (DateTime.TryParse(value.ToString(), out i))
                        return i;

                    return DateTime.Now;
                }
                else
                {
                    return DateTime.Now;
                }
            }
            catch (Exception)
            {
                return DateTime.Now;
            }
        }

        public static DateTime dateParse(object value, DateTime defaultValue)
        {
            try
            {
                if (value != null)
                {
                    DateTime i;
                    if (DateTime.TryParse(value.ToString(), out i))
                        return i;
                    else
                        return defaultValue;
                }
                else
                {
                    return defaultValue;
                }
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static bool HasRows(DataTable dataTable)
        {
            if (dataTable == null)
                return false;

            if (dataTable.Rows.Count > 0)
                return true;
            else
                return false;
        }

        public static List<Dictionary<string, object>> DataTableToDictionaryList(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();

            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    dict[col.ColumnName] = row[col];
                }
                list.Add(dict);
            }

            return list;
        }

        private static string replace_values(object value)
        {
            return value.ToString().Replace(",", "").Replace("_", "").Replace("(", "-").Replace(")", "");
        }
    }
}
