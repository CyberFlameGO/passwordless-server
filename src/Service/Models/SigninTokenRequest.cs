using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Passwordless.Service.Extensions;

namespace Passwordless.Service.Models;

public class SigninTokenRequest : RequestBase
{
    private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromSeconds(120);

    [Required(AllowEmptyStrings = false)]
    public required string UserId { get; init; }

    /// <summary>
    /// Time to live is the number of seconds the token has before it expires.
    /// </summary>
    [JsonPropertyName("timeToLive")]
    [Obsolete("This property is only used for serialization.")]
    [Range(1, 604800)]
    public int? TimeToLiveSeconds { get; init; }

    [JsonIgnore]
    public TimeSpan TimeToLive => TimeToLiveSeconds?.ToTimeSpanFromSeconds() ?? DefaultTimeToLive;
};