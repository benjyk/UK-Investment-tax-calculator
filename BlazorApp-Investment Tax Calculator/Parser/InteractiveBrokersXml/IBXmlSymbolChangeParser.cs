using InvestmentTaxCalculator.Model.TaxEvents;

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

public static class IBXmlSymbolChangeParser
{
    public static IList<SymbolChange> ParseXml(XElement document)
    {
        IEnumerable<XElement> filteredElements = document.Descendants("CorporateAction")
            .Where(row => row.GetAttribute("type") == "IC")
            .Where(row => !IsNegativeQuantity(row));
        return filteredElements.Select(SymbolChangeMaker).Where(sc => sc != null).ToList()!;
    }

    private static SymbolChange? SymbolChangeMaker(XElement element)
    {
        string description = element.GetAttribute("description");
        string matchExpression = @"^(\w+)\(";
        Regex regex = new(matchExpression, RegexOptions.Compiled);
        Match matchResult = regex.Match(description);
        if (!matchResult.Success) return null;

        string oldSymbol = matchResult.Groups[1].Value;
        string newSymbol = element.GetAttribute("symbol");

        if (oldSymbol == newSymbol) return null;

        return new SymbolChange
        {
            AssetName = newSymbol,
            Date = XmlParserHelper.ParseDate(element.GetAttribute("dateTime")),
            OldAssetName = oldSymbol,
        };
    }

    private static bool IsNegativeQuantity(XElement element)
    {
        string quantity = element.GetAttribute("quantity");
        return !string.IsNullOrEmpty(quantity) && decimal.TryParse(quantity, out decimal qty) && qty < 0;
    }
}
