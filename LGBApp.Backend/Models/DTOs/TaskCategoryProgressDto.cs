namespace LGBApp.Backend.Models.DTOs;

public class TaskCategoryProgressDto
{
    public string Category { get; set; } = string.Empty;
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Total { get; set; }
}
