using System.Text.Json.Serialization;

namespace LupiraHealthApi.Domain;

/// <summary>Kind of registered device that may feed this health record.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeviceKind>))]
public enum DeviceKind { SmartRing, Phone, Watch, Scale, BloodPressureCuff, Other }
