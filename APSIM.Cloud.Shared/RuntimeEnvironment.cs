namespace APSIM.Cloud.Shared
{
    /// <summary>
    /// Specifies a environment to run APSIM in.
    /// </summary>
    public class RuntimeEnvironment
    {
        /// <summary>
        /// Gets or sets the name of the APSIM classic revision to use e.g. Build number "Apsim7.9-R4058".
        /// If this is null then ApsimXBuildNumner will be used. If both are null, old style YP
        /// run directory will be used.
        /// </summary>
        public string APSIMRevision { get; set; }

        /// <summary>
        /// Gets or sets the name of the APSIM next generation pull request id to use (issue number) e.g. 1852. 
        /// If this is null then APSIMRevision will be used. If both are null, old style YP
        /// run directory will be used.
        /// </summary>
        public int APSIMxBuildNumber { get; set; }

        /// <summary>
        /// Gets or sets the name of the AusFarm revision to use e.g. "Ausfarm1.4.12".
        /// </summary>
        public string AusfarmRevision { get; set; }

        /// <summary>
        /// Gets or sets the optional, additional runtime packages to use. These are extra or 
        /// replacement files that will be added to the specified APSIM build. Can be null.
        /// </summary>
        public string[] RuntimePackages { get; set; }
    }
}
