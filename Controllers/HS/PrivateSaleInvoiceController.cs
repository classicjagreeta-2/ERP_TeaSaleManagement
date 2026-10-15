namespace TeaSaleManagement.Controllers
{
    // "5.1 Private Sale" (VB trn_salesInvoice.frm with pSale_tag "", ORDERCODE 02040501T).
    // Everything lives in TeaSaleInvoiceControllerBase; views: Views/TeaSaleInvoice.
    public class PrivateSaleInvoiceController : TeaSaleInvoiceControllerBase
    {
        protected override string Screen => "PrivateSaleInvoice";   // AEDV row + record-lock screen
        protected override string Tag => "P";                        // API tag (VB pSale_tag "")
        protected override string Caption => "Private Sale";
    }
}
