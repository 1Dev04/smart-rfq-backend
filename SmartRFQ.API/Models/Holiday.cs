namespace SmartRFQ.API.Models;

public class Holiday
{
    public int Id {get; set;}
    public string Name {get; set;} = "";
    public DateOnly HolidayDate {get; set;}
    public DateTime CreatedAt {get; set;}
}