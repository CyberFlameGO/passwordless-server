using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Passwordless.Common.Backup;

public class CsvBackupSerializer : IBackupSerializer
{
    private readonly CsvConfiguration _configuration = new(CultureInfo.InvariantCulture)
    {
        AllowComments = false,
        CountBytes = true,
        HasHeaderRecord = true,
        IgnoreBlankLines = true,
        IncludePrivateMembers = false,
        InjectionOptions = InjectionOptions.None,
        Mode = CsvMode.RFC4180,
        Encoding = Encoding.UTF8,
        IgnoreReferences = true
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CsvBackupSerializer> _logger;

    public CsvBackupSerializer(
        IServiceProvider serviceProvider,
        ILogger<CsvBackupSerializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string Serialize<TEntity>(IReadOnlyCollection<TEntity> entities)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, _configuration))
        {
            var classMap = GetClassMap<TEntity>();
            csv.Context.RegisterClassMap(classMap);
            csv.WriteRecords(entities);
            return writer.ToString();
        }
    }

    public IEnumerable<TEntity> Deserialize<TEntity>(string input)
    {
        using (var reader = new StringReader(input))
        using (var csv = new CsvReader(reader, _configuration))
        {
            var classMap = GetClassMap<TEntity>();
            csv.Context.RegisterClassMap(classMap);
            var records = csv.GetRecords<TEntity>();
            return records.ToList();
        }
    }

    private ClassMap<TEntity> GetClassMap<TEntity>()
    {
        try
        {
            return _serviceProvider.GetRequiredService<ClassMap<TEntity>>();
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("Failed to get class map for {EntityType}", typeof(TEntity).Name);
            throw new ConfigurationException($"Failed to get class map for {typeof(TEntity).Name}. Did you add an entity and register the class map in the service provider?");
        }
    }
}