using InvestmentTaxCalculator.Model.TaxEvents;

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

public static class IBXmlSymbolChangeParser
{
    public static IList<SymbolChange> ParseXml(XElement document)
    {
        // For IC (ISIN Change) corporate actions, IBKR provides two rows:
        // - Negative quantity row: symbol attribute contains the NEW symbol
        // - Positive quantity row: symbol attribute contains the OLD symbol (often with ".OLD" suffix)
        // We use negative quantity rows to get the correct new symbol.
        IEnumerable<XElement> filteredElements = document.Descendants("CorporateAction")
            .Where(row => row.GetAttribute("type") == "IC")
            .Where(row => IsNegativeQuantity(row));
        return filteredElements.Select(SymbolChangeMaker).Where(sc => sc != null).ToList()!;
    }

    private static SymbolChange? SymbolChangeMaker(XElement element)
    {
        string description = element.GetAttribute("description");
        // Match everything before the first '(' - handles symbols with spaces like "GLEO WS"
        string matchExpression = @"^(.+?)\(";
        Regex regex = new(matchExpression, RegexOptions.Compiled);
        Match matchResult = regex.Match(description);
        if (!matchResult.Success) return null;

        string oldSymbol = matchResult.Groups[1].Value.Trim();
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
