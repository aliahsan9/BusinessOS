using System.Text;
using BusinessOS.Application.Common.Interfaces;
// StringBuilder used for CSV line parsing
using BusinessOS.Application.Features.DataImports;
using BusinessOS.Application.Features.DataImports.Services;
using ClosedXML.Excel;
using MediatR;

namespace BusinessOS.Application.Features.DataImports.Queries.GetImportTemplate;

public sealed record GetImportTemplateQuery(string EntityType, string? Format = null) : IRequest<byte[]>;

public sealed class GetImportTemplateQueryHandler : IRequestHandler<GetImportTemplateQuery, byte[]>
{
    private readonly ICurrentUserService _user;

    public GetImportTemplateQueryHandler(ICurrentUserService user)
    {
        _user = user;
    }

    public Task<byte[]> Handle(GetImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var entityType = DataImportEntityTypes.NormalizeOrThrow(request.EntityType);
        DataImportAccess.EnsureCanAccess(entityType, _user, write: false);

        if (string.Equals(request.Format, "xlsx", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(BuildXlsx(entityType));

        var csv = ImportEntityDefinitions.BuildTemplateCsv(entityType);
        return Task.FromResult(Encoding.UTF8.GetBytes(csv));
    }

    private static byte[] BuildXlsx(string entityType)
    {
        var fields = ImportEntityDefinitions.GetFields(entityType);
        var csv = ImportEntityDefinitions.BuildTemplateCsv(entityType);
        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(entityType);
        for (var i = 0; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            for (var c = 0; c < cols.Count; c++)
                sheet.Cell(i + 1, c + 1).Value = cols[c];
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
