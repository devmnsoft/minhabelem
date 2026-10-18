using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
namespace MinhaBelem.Tests;
[Collection("MySql")]
public sealed class MediaLibraryTests(PortalFixture fixture)
{
    [MySqlFact]public async Task LicensedUploadStaysPrivateUntilAssociatedContentIsPublished()
    {
        var(client,actor)=await new OperationalTests(fixture).Login();using(client)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var repo=scope.ServiceProvider.GetRequiredService<IContentRepository>();var item=new ContentItem{Title="Fixture de mídia",Summary="Arquivo de teste autorizado",SourceName="Fixture",SourceUrl="https://example.com/"+Guid.NewGuid(),Slug=Guid.NewGuid().ToString("N"),AuthorName="Teste",VerificationStatus=VerificationStatus.Verified};await repo.InsertAsync(item,actor,"Fixture",default);
            var token=PortalFixture.Token(await client.GetStringAsync("/admin/midias"));var bytes=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","poster.png"));
            using var form=new MultipartFormDataContent();form.Add(new StringContent(token),"__RequestVerificationToken");form.Add(new StringContent(item.Id.ToString()),"contentId");form.Add(new StringContent("Cartaz da fixture de teste"),"description");form.Add(new StringContent("Fixture"),"credit");form.Add(new StringContent("Uso autorizado exclusivamente em testes"),"license");form.Add(new StringContent("true"),"reproductionAuthorized");form.Add(new ByteArrayContent(bytes),"image","poster.png");
            Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/admin/midias",form)).StatusCode);
            await using var db=await fixture.OpenAsync();var media=await db.QuerySingleAsync<(long Id,string Key)>("SELECT id AS Id,storage_key AS `Key` FROM mb_media_assets WHERE content_id=@id",new{id=item.Id});
            try
            {
                Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/midia/"+media.Id)).StatusCode);
                item=(await repo.GetAsync(item.Id,default))!;item.Status=ContentStatus.Published;Assert.True(await repo.UpdateAsync(item,actor,"Publicação controlada da fixture",default));
                var response=await client.GetAsync("/midia/"+media.Id);Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("image/png",response.Content.Headers.ContentType?.MediaType);Assert.Equal(bytes,await response.Content.ReadAsByteArrayAsync());
            }
            finally
            {
                var env=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();var root=Path.GetFullPath(Path.Combine(env.ContentRootPath,"App_Data","media"));var file=Path.GetFullPath(Path.Combine(root,media.Key));Assert.StartsWith(root+Path.DirectorySeparatorChar,file);File.Delete(file);
            }
        }
    }
}
