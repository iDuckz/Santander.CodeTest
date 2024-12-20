using Microsoft.AspNetCore.Mvc;
using Santander.CodeTest.DTO;
using Santander.CodeTest.Entities;
using System.Text.Json;

namespace Santander.CodeTest.Controllers
{
    public class StoryController : Controller
    {
        const string AllIdsUrl = "/v0/beststories.json";
        const string StoryItemUrl = "/v0/item/{0}.json";
        private const int MaxSemaphoreQueue = 50;

        private IHttpClientFactory _httpClientFactory;
        public StoryController(IHttpClientFactory httpFactory)
        {
            _httpClientFactory = httpFactory;
        }

        [HttpGet]
        [Route("/api/Story/GetStories")]
        public async Task<IActionResult> GetStories(int n)
        {
            try
            {
                if (n == 0)
                    return StatusCode(StatusCodes.Status400BadRequest, "The parameter n must be greater than 0.");

                var client = _httpClientFactory.CreateClient("HackerNews");
                var responseListIds = await client.GetStringAsync(AllIdsUrl);
                List<int> idList;
                if (responseListIds != null)
                {
                    idList = JsonSerializer.Deserialize<List<int>>(responseListIds);
                }
                else
                    return StatusCode(StatusCodes.Status500InternalServerError, "The list of stories id is empty");

                var semaphore = new SemaphoreSlim(MaxSemaphoreQueue);
                List<Story> stories = new List<Story>();
                List<Task> requests = new List<Task>();

                idList = idList.Take(n).ToList();
                idList.ForEach(idList =>
                {
                    requests.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var response = await client.GetStringAsync(string.Format(StoryItemUrl, idList));
                            if (response != null)
                            {
                                Story story = JsonSerializer.Deserialize<Story>(response);
                                stories.Add(story);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                        
                    }));
                });

                await Task.WhenAll(requests);

                List<StoryDTO> storiesDto = stories.Select(story =>
                {
                    return new StoryDTO
                    {
                        title = story.title,
                        uri = story.url,
                        postedBy = story.by,
                        time = DateTimeOffset.FromUnixTimeSeconds(story.time).UtcDateTime,
                        score = story.score, 
                        commentCount = story.commentCount //The property CommentCount is not coming from the API
                    };
                }).ToList();

                return Ok(storiesDto.OrderByDescending(s=> s.score));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
