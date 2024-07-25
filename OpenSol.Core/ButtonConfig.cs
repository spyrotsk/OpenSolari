
namespace OpenSol.Core
{
    /// <summary>
    /// Configuration for a client-side button.
    /// </summary>
    public class ButtonConfig
    {
        /// <summary>
        /// ID that must match the server
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Description/text of the button
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Text color (#RRGGBB format, optional)
        /// </summary>
        public string? TextColor { get; set; }

        /// <summary>
        /// Background color (#RRGGBB format, optional)
        /// </summary>
        public string? BackgroundColor { get; set; }
    }

    /// <summary>
    /// Container for button list
    /// </summary>
    public class ButtonsConfig
    {
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();
    }
}
