using System.Data;
using System.Globalization;
using System.Reflection;

namespace NeoEditor.Helpers.Converters;

public static class DataTableToEntity<T> where T : new()
{
    
    
    public static List<T> FillModel(DataTable dt)
    {
        if (dt == null || dt.Rows.Count == 0)
            return null;
        var result = new List<T>();
        foreach (DataRow dr in dt.Rows)
            try
            {
                var res = new T();
                for (var i = 0; i < dr.Table.Columns.Count; i++)
                {
                    var propertyInfo = res.GetType().GetProperty(dr.Table.Columns[i].ColumnName,
                        BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (propertyInfo != null && dr[i] != DBNull.Value)
                    {
                        var value = dr[i];
                        switch (propertyInfo.PropertyType.FullName)
                        {
                            case "System.Double":
                                propertyInfo.SetValue(res, Math.Round(Convert.ToDouble(value), 2), null); break;
                            case "System.Decimal":
                                propertyInfo.SetValue(res, Convert.ToDecimal(value), null); break;
                            case "System.Boolean":
                                if (value is string s)
                                {
                                    propertyInfo.SetValue(res, Convert.ToBoolean(s == "0"? 0:1), null); break;
                                }else if (value is int n)
                                {
                                    propertyInfo.SetValue(res, Convert.ToBoolean(n), null); break;
                                }
                                else
                                {
                                    propertyInfo.SetValue(res, Convert.ToBoolean(value), null);
                                }

                                break;
                            case "System.String":
                                propertyInfo.SetValue(res, value, null); break;
                            case "System.Int32":
                                propertyInfo.SetValue(res, Convert.ToInt32(value), null); break;
                            default:
                                propertyInfo.SetValue(res, value, null); break;
                        }
                    }
                }

                result.Add(res);
            }
            catch (Exception ex)
            {
            }

        return result;
    }
}