namespace TeaSaleManagement.Controllers
{
    // HS menu "Masters": one controller per menu row (its own AEDV row + record lock); everything else is in
    // FactoryMasterControllerBase, the views in Views/FactoryMaster, the fields and rules in api/FactoryMaster.

    // Vehicle Type Master (VB mast_vehicleTyp.frm, M_VTYPE)
    public class VehicleTypeMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "VehicleTypeMaster";
        protected override string Master => "vehicletype";
    }

    // Vehicle Master (VB mast_vehicle.frm + the doc's new fields, M_VEHMAS, by unit)
    public class VehicleMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "VehicleMaster";
        protected override string Master => "vehicle";
    }

    // Transporter Master (VB mast_transp.frm, M_TRANSP)
    public class TransporterMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "TransporterMaster";
        protected override string Master => "transporter";
    }

    // Clerk Master (new, M_CLERK, by unit; linked to the payroll employee master)
    public class ClerkMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "ClerkMaster";
        protected override string Master => "clerk";
    }

    // Route Master (new, M_ROUTE, by unit; no Farmer ID since 15/10/26)
    public class RouteMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "RouteMaster";
        protected override string Master => "route";
    }

    // Centre Master (new, M_CENTRE, by unit)
    public class CentreMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "CentreMaster";
        protected override string Master => "centre";
    }

    // Weighment Point Master (new 15/10/26, M_WEIGHMENT_POINT, by unit)
    public class WeighmentPointMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "WeighmentPointMaster";
        protected override string Master => "weighment";
    }

    // Driver Master (new, M_DRIVER)
    public class DriverMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "DriverMaster";
        protected override string Master => "driver";
    }

    // Helper/Loader Master (new, M_HELPER)
    public class HelperMasterController : FactoryMasterControllerBase
    {
        protected override string Screen => "HelperMaster";
        protected override string Master => "helper";
    }
}
