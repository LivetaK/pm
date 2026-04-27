using System.Data;
using Dapper;

namespace pm.Infrastructure;

public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value  = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) =>
        value is DateOnly d ? d : DateOnly.FromDateTime(Convert.ToDateTime(value));
}
