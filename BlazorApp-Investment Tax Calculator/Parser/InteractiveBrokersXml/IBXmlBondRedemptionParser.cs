using InvestmentTaxCalculator.Enumerations;
using InvestmentTaxCalculator.Model;
using InvestmentTaxCalculator.Model.TaxEvents;

using System.Xml.Linq;

namespace InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

/// <summary>
/// Parses BM (Bond Maturity) corporate actions from IBKR XML.
/// These represent bond redemptions/maturities where bonds are redeemed for cash.
/// Quantity is negative (bonds removed), proceeds is positive (cash received).
/// </summary>
public static class IBXmlBondRedemptionParser
{
    public static IList<Trade> ParseXml(XElement document)
    {
        // BM = Bond Maturity (redemptions, early calls)
        // Only process BOND asset category
        IEnumerable<XElement> filteredElements = document.Descendants("CorporateAction")
            .Where(row => row.GetAttribute("type") == "BM")
            .Where(row => row.GetAttribute("assetCategory") == "BOND")
            .GroupBy(row => row.GetAttribute("transactionID"))
            .Select(group => group.First());

        return filteredElements.Select(element => XmlParserHelper.ParserExceptionManager(TradeMaker, element)).Where(trade => trade != null).ToList()!;
    }

    private static Trade? TradeMaker(XElement element)
    {
        // Quantity is negative in XML (bonds removed from position)
        decimal quantity = Math.Abs(decimal.Parse(element.GetAttribute("quantity")));
        // Proceeds is positive (cash received)
        decimal proceeds = decimal.Parse(element.GetAttribute("proceeds"));
        string currency = element.GetAttribute("currency");
        decimal fxRate = decimal.Parse(element.GetAttribute("fxRateToBase"));

        return new Trade
        {
            AssetType = AssetCategoryType.BOND,
            AcquisitionDisposal = TradeType.DISPOSAL,
            AssetName = element.GetAttribute("symbol"),
            Description = $"Bond redemption: {element.GetAttribute("description")}",
            Date = XmlParserHelper.ParseDate(element.GetAttribute("dateTime")),
            Quantity = quantity,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(proceeds, currency),
                Description = "Redemption proceeds",
                FxRate = fxRate
            },
            Expenses = [],
            TradeReason = TradeReason.CorporateAction,
            Isin = element.GetAttribute("isin")
        };
    }
}
