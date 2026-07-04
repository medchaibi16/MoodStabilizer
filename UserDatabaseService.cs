using System;
using System.Collections.Generic;
using Npgsql;

namespace MoodStabilizer
{
    public static class UserDatabaseService
    {
        // PostgreSQL connection string
        private const string ConnectionString = "Host=localhost;Username=postgres;Password=root;Database=postgres;Port=5432;";
        public static List<User> GetAllUsers()
        {
            var users = new List<User>();

            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = "SELECT userid, adminid, fullname, email, passwordhash, faceembedding, facephotopath FROM users ORDER BY fullname";

                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new User
                                {
                                    UserId = (int)reader["userid"],
                                    AdminId = reader["adminid"] as int?,
                                    FullName = reader["fullname"].ToString() ?? "Unknown",
                                    Email = reader["email"].ToString() ?? string.Empty,
                                    PasswordHash = reader["passwordhash"].ToString() ?? string.Empty,
                                    FaceEmbedding = reader["faceembedding"] as string,
                                    FacePhotoPath = reader["facephotopath"] as string
                                });
                            }
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                throw new Exception($"Database error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error: {ex.Message}", ex);
            }

            return users;
        }

        public static User? GetUserById(int userId)
        {
            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = "SELECT userid, adminid, fullname, email, passwordhash, faceembedding, facephotopath FROM users WHERE userid = @UserId";

                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);

                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new User
                                {
                                    UserId = (int)reader["userid"],
                                    AdminId = reader["adminid"] as int?,
                                    FullName = reader["fullname"].ToString() ?? "Unknown",
                                    Email = reader["email"].ToString() ?? string.Empty,
                                    PasswordHash = reader["passwordhash"].ToString() ?? string.Empty,
                                    FaceEmbedding = reader["faceembedding"] as string,
                                    FacePhotoPath = reader["facephotopath"] as string
                                };
                            }
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                throw new Exception($"Database error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error: {ex.Message}", ex);
            }

            return null;
        }
    }
}