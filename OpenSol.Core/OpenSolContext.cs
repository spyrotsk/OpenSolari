using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace OpenSol.Core
{
    [JsonSerializable(typeof(DevicesConfig))]
    [JsonSerializable(typeof(DeviceConfig))]
    [JsonSerializable(typeof(ButtonsConfig))]
    [JsonSerializable(typeof(ButtonConfig))]
    [JsonSerializable(typeof(AccessControlConfig))]
    [JsonSerializable(typeof(UserAccessRule))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
    internal partial class OpenSolContext : JsonSerializerContext
    {
    }
}
