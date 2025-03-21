namespace MongoRepository.Core.Settings;

/// <summary>
/// Settings for MongoDB connection
/// </summary>
public class MongoDbSettings : IMongoDbSettings
{
    /// <summary>
    /// Gets or sets the connection string to the MongoDB server
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Gets or sets the database name
    /// </summary>
    public string DatabaseName { get; set; } = null!;
}

/// <summary>
/// Interface for MongoDB settings
/// </summary>
public interface IMongoDbSettings
{
    /// <summary>
    /// Gets or sets the connection string to the MongoDB server
    /// </summary>
    string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database name
    /// </summary>
    string DatabaseName { get; set; }
}