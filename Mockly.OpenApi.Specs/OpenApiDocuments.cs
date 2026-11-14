namespace Mockly.OpenApi.Specs;

/// <summary>
/// Writes OpenAPI documents to temporary files, so that the specs can keep the document next to the test.
/// </summary>
internal static class OpenApiDocuments
{
    public static string WriteToFile(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"mockly-openapi-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>
    /// An OpenAPI 3.0 pet store that covers path, query and header parameters, request bodies and responses
    /// with and without examples.
    /// </summary>
    public const string PetStore =
        """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pet store", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.com/v1" } ],
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "parameters": [
                  { "name": "limit", "in": "query", "required": true, "schema": { "type": "integer", "minimum": 1, "maximum": 100 } },
                  { "name": "status", "in": "query", "schema": { "type": "string", "enum": [ "available", "sold" ] } }
                ],
                "responses": {
                  "200": {
                    "description": "The pets",
                    "content": {
                      "application/json": {
                        "schema": { "type": "array", "items": { "$ref": "#/components/schemas/Pet" } },
                        "example": [ { "id": 1, "name": "Rex", "status": "available" } ]
                      }
                    }
                  }
                }
              },
              "post": {
                "operationId": "createPet",
                "parameters": [
                  { "name": "X-Tenant", "in": "header", "required": true, "schema": { "type": "string" } }
                ],
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": { "schema": { "$ref": "#/components/schemas/NewPet" } }
                  }
                },
                "responses": {
                  "400": { "description": "Invalid pet" },
                  "201": {
                    "description": "Created",
                    "content": {
                      "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } }
                    }
                  }
                }
              }
            },
            "/pets/mine": {
              "get": {
                "operationId": "listMyPets",
                "responses": {
                  "200": {
                    "description": "My pets",
                    "content": { "application/json": { "example": [ { "id": 99, "name": "Mine" } ] } }
                  }
                }
              }
            },
            "/pets/{petId}": {
              "parameters": [
                { "name": "petId", "in": "path", "required": true, "schema": { "type": "integer", "format": "int64" } }
              ],
              "get": {
                "operationId": "getPet",
                "responses": {
                  "200": {
                    "description": "The pet",
                    "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } }
                  }
                }
              },
              "delete": {
                "operationId": "deletePet",
                "responses": { "204": { "description": "Deleted" } }
              }
            }
          },
          "components": {
            "schemas": {
              "Pet": {
                "type": "object",
                "required": [ "id", "name" ],
                "properties": {
                  "id": { "type": "integer", "format": "int64", "readOnly": true },
                  "name": { "type": "string", "minLength": 1, "maxLength": 20 },
                  "status": { "type": "string", "enum": [ "available", "sold" ] },
                  "birthDate": { "type": "string", "format": "date" },
                  "chipId": { "type": "string", "format": "uuid" },
                  "nickname": { "type": "string", "nullable": true },
                  "tags": { "type": "array", "minItems": 1, "items": { "type": "string" } },
                  "weight": { "type": "number", "minimum": 0.5 },
                  "parent": { "$ref": "#/components/schemas/Pet" }
                }
              },
              "NewPet": {
                "type": "object",
                "required": [ "name" ],
                "additionalProperties": false,
                "properties": {
                  "name": { "type": "string", "minLength": 1, "maxLength": 20 },
                  "status": { "type": "string", "enum": [ "available", "sold" ] },
                  "nickname": { "type": "string", "nullable": true },
                  "tags": { "type": "array", "maxItems": 2, "items": { "type": "string" } }
                }
              }
            }
          }
        }
        """;
}
