using InvestmentTaxCalculator.Model.Interfaces;
using InvestmentTaxCalculator.Model.UkTaxModel;

namespace InvestmentTaxCalculator.Model.TaxEvents;

/// <summary>
/// Represents a CUSIP/ISIN change corporate action (e.g. de-SPAC).
/// The share quantity and cost basis are unchanged, but the asset is renamed.
/// </summary>
public record SymbolChange : CorporateAction
{
    /// <summary>
    /// The symbol before the change (e.g. the SPAC ticker).
    /// </summary>
    public required string OldAssetName { get; init; }

    public override string Reason => $"{OldAssetName} renamed to {AssetName} on {Date:d}";

    public override MatchAdjustment TradeMatching(ITradeTaxCalculation trade1, ITradeTaxCalculation trade2, MatchAdjustment matchAdjustment)
    {
        return matchAdjustment;
    }

    public override void ChangeSection104(UkSection104 section104)
    {
    }

    public override string GetDuplicateSignature()
    {
        return $"SYMBOLCHANGE|{Date.Ticks}|{OldAssetName}|{AssetName}";
    }
}
