using System.Net;
using System.Text;
using ImmichFrame.Core.Api;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Api;

[TestFixture]
public class AlbumResponseCompatibilityTests
{
    private const string CurrentAlbumResponse = """
        [
          {
            "albumName": "Frame photos",
            "albumThumbnailAssetId": null,
            "albumUsers": [
              {
                "role": "owner",
                "user": {
                  "avatarColor": "primary",
                  "email": "owner@example.com",
                  "id": "11111111-1111-4111-8111-111111111111",
                  "name": "Owner",
                  "profileChangedAt": "2026-07-01T00:00:00.000Z",
                  "profileImagePath": ""
                }
              }
            ],
            "assetCount": 1,
            "createdAt": "2026-07-01T00:00:00.000Z",
            "description": "",
            "hasSharedLink": false,
            "id": "22222222-2222-4222-8222-222222222222",
            "isActivityEnabled": true,
            "shared": false,
            "updatedAt": "2026-07-01T00:00:00.000Z"
          }
        ]
        """;

    private const string CurrentSearchResponse = """
        {
          "albums": {
            "count": 0,
            "facets": [],
            "items": [],
            "total": 0
          },
          "assets": {
            "count": 1,
            "facets": [],
            "items": [
              {
                "checksum": "Y2hlY2tzdW0=",
                "createdAt": "2026-07-01T00:00:00.000Z",
                "duration": null,
                "fileCreatedAt": "2026-07-01T00:00:00.000Z",
                "fileModifiedAt": "2026-07-01T00:00:00.000Z",
                "hasMetadata": true,
                "height": 1080,
                "id": "33333333-3333-4333-8333-333333333333",
                "isArchived": false,
                "isEdited": false,
                "isFavorite": false,
                "isOffline": false,
                "isTrashed": false,
                "localDateTime": "2026-07-01T00:00:00.000Z",
                "originalFileName": "photo.jpg",
                "originalPath": "upload/photo.jpg",
                "ownerId": "11111111-1111-4111-8111-111111111111",
                "people": [
                  {
                    "birthDate": null,
                    "id": "44444444-4444-4444-8444-444444444444",
                    "isHidden": false,
                    "name": "Person",
                    "thumbnailPath": "thumb.jpg"
                  }
                ],
                "thumbhash": null,
                "type": "IMAGE",
                "updatedAt": "2026-07-01T00:00:00.000Z",
                "visibility": "timeline",
                "width": 1920
              }
            ],
            "nextPage": null,
            "total": 1
          }
        }
        """;

    private const string CurrentPeopleResponse = """
        {
          "hasNextPage": false,
          "hidden": 0,
          "people": [
            {
              "birthDate": null,
              "id": "44444444-4444-4444-8444-444444444444",
              "isHidden": false,
              "name": "Alex Example",
              "thumbnailPath": "thumb.jpg",
              "updatedAt": "2026-07-01T00:00:00.000Z"
            }
          ],
          "total": 1
        }
        """;

    [Test]
    public async Task GetAllAlbums_accepts_owner_role_and_current_summary_shape()
    {
        using var httpClient = new HttpClient(new JsonResponseHandler(CurrentAlbumResponse));
        var api = new ImmichApi("https://immich.example", httpClient);

        var albums = await api.GetAllAlbumsAsync(null, null);

        var album = albums.Single();
        Assert.Multiple(() =>
        {
            Assert.That(album.AlbumName, Is.EqualTo("Frame photos"));
            Assert.That(album.AlbumUsers.Single().Role, Is.EqualTo(AlbumUserRole.Owner));
            Assert.That(album.AssetCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DeleteAlbum_accepts_no_content_response()
    {
        using var httpClient = new HttpClient(new NoContentResponseHandler());
        var api = new ImmichApi("https://immich.example", httpClient);

        Assert.DoesNotThrowAsync(() => api.DeleteAlbumAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task SearchMetadata_accepts_current_asset_and_people_shape()
    {
        using var httpClient = new HttpClient(new JsonResponseHandler(CurrentSearchResponse));
        var api = new ImmichApi("https://immich.example", httpClient);

        var result = await api.SearchMetadataAsync(new MetadataSearchDto());

        var asset = result.Assets.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(asset.Id, Is.EqualTo("33333333-3333-4333-8333-333333333333"));
            Assert.That(asset.People.Single().Id, Is.EqualTo("44444444-4444-4444-8444-444444444444"));
        });
    }

    [Test]
    public async Task GetAllPeople_accepts_named_people_response()
    {
        using var httpClient = new HttpClient(new JsonResponseHandler(CurrentPeopleResponse));
        var api = new ImmichApi("https://immich.example", httpClient);

        var result = await api.GetAllPeopleAsync(1, 250, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasNextPage, Is.False);
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.People.Single().Name, Is.EqualTo("Alex Example"));
        });
    }

    private sealed class JsonResponseHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };

            return Task.FromResult(response);
        }
    }

    private sealed class NoContentResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                RequestMessage = request,
            };

            return Task.FromResult(response);
        }
    }
}
