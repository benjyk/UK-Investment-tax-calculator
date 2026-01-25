using InvestmentTaxCalculator.Enumerations;
using InvestmentTaxCalculator.Model.TaxEvents;

using System.Xml.Linq;

namespace InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

public static class IBXmlOptionTradeParser
{
    private static readonly HashSet<string> _optionAssetCategories = ["OPT", "FOP", "FSFOP", "WAR"];

    public static IList<OptionTrade> ParseXml(XElement document)
    {
        IEnumerable<XElement> filteredElements = document.Descendants("Order").Where(row => row.GetAttribute("levelOfDetail") == "ORDER" &&
                                                                                                             _optionAssetCategories.Contains(row.GetAttribute("assetCategory")));
        return filteredElements.Select(element => XmlParserHelper.ParserExceptionManager(OptionTradeMaker, element))
                                                                                          .Where(trade => trade != null).ToList()!;

    }

    private static OptionTrade? OptionTradeMaker(XElement element)
    {
        return new OptionTrade
        {
            AcquisitionDisposal = element.GetTradeType(),
            AssetName = element.GetAttribute("symbol"),
            Description = element.GetAttribute("description"),
            Date = XmlParserHelper.ParseDate(element.GetAttribute("dateTime")),
            Quantity = element.GetQuantity(),
            GrossProceed = element.GetGrossProceed(),
            Expenses = element.BuildExpenses(),
            Underlying = element.GetAttribute("underlyingSymbol"),
            StrikePrice = element.BuildMoney("strike", "currency"),
            ExpiryDate = XmlParserHelper.ParseDate(element.GetAttribute("expiry")),
            Multiplier = decimal.Parse(element.GetAttribute("multiplier")),
            PUTCALL = GetPutCall(element),
            TradeReason = element.GetAttribute("notes") switch
            {
                string s when s.Split(";").Contains("Ex") => TradeReason.OwnerExerciseOption,
                string s when s.Split(";").Contains("A") => TradeReason.OptionAssigned,
                string s when s.Split(";").Contains("Ep") => TradeReason.Expired,
                _ => TradeReason.OrderedTrade
            },
            Isin = element.GetAttribute("isin")
        };
    }

    private static PUTCALL GetPutCall(XElement element)
    {
        string putCallValue = element.GetAttribute("putCall");
        string assetCategory = element.GetAttribute("assetCategory");

        return putCallValue switch
        {
            "C" => PUTCALL.CALL,
            "P" => PUTCALL.PUT,
            // Warrants are call options - default to CALL if putCall is empty
            "" when assetCategory == "WAR" => PUTCALL.CALL,
            _ => throw new ParseException($"Unknown putCall '{putCallValue}' for {element}")
        };
    }
}

