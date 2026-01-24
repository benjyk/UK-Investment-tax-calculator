using InvestmentTaxCalculator.Model.TaxEvents;

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

public static class IBXmlStockSplitParser
{
    private static readonly HashSet<string> _splitTypes = ["FS", "RS", "FI"];

    public static IList<StockSplit> ParseXml(XElement document)
    {
        IEnumerable<XElement> filteredElements = document.Descendants("CorporateAction")
            .Where(row => _splitTypes.Contains(row.GetAttribute("type")))
            .Where(row => !row.GetAttribute("symbol").EndsWith(".OLD"));
        return filteredElements.Select(StockSplitMaker).Where(split => split != null).ToList()!;
    }

    private static StockSplit StockSplitMaker(XElement element)
    {
        string matchExpression = @"SPLIT (\d*) FOR (\d*)";
        string description = element.GetAttribute("description");
        Regex regex = new(matchExpression, RegexOptions.Compiled);
        Match matchResult = regex.Match(description);
        return new StockSplit
        {
            AssetName = element.GetAttribute("symbol"),
            Date = XmlParserHelper.ParseDate(element.GetAttribute("dateTime")),
            SplitFrom = ushort.Parse(matchResult.Groups[2].Value),
            SplitTo = ushort.Parse(matchResult.Groups[1].Value),
        };

    }
}
