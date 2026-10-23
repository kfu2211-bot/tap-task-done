using System.Collections.ObjectModel;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MySqlConnector;

namespace tts.Data;

// Stores key-ring XML (encrypted by Data Protection) in the existing database.
public sealed class MySqlKeyRepository(string connectionString) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT `Xml` FROM `data_protection_keys` ORDER BY `Id`";
        using var reader = command.ExecuteReader();
        var keys = new List<XElement>();
        while (reader.Read()) keys.Add(XElement.Parse(reader.GetString(0)));
        return new ReadOnlyCollection<XElement>(keys);
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO `data_protection_keys` (`FriendlyName`, `Xml`) VALUES (@name, @xml)";
        command.Parameters.AddWithValue("@name", friendlyName);
        command.Parameters.AddWithValue("@xml", element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
