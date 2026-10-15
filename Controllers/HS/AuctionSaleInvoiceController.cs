namespace TeaSaleManagement.Controllers
{
    // "5.3 Auction Sale" (VB trn_salesInvoice.frm with pSale_tag "4", ORDERCODE 02040503T).
    // Everything lives in TeaSaleInvoiceControllerBase; views: Views/TeaSaleInvoice.
    public class AuctionSaleInvoiceController : TeaSaleInvoiceControllerBase
    {
        protected override string Screen => "AuctionSaleInvoice";   // AEDV row + record-lock screen
        protected override string Tag => "4";                        // API tag (VB pSale_tag "4")
        protected override string Caption => "Auction Sale";
    }
}
