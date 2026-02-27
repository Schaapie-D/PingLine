namespace PingLine.Notification.Notifications;

internal class PLNTime : IPingLineNotifier
{
    public const string Name = "time";
    public const string TypeName = Name;
    public string id { get; set; }
    public ConsoleColor TextColor { get; set; } = ConsoleColor.White;

    public TimeSpan TriggerTime;
    public string TimeText = null!;

    private DateTime nextTriggerTime;

    public PLNTime(string id, bool askArgs = true)
    {
        this.id = id;

        if(!askArgs) return;

        Console.WriteLine("The time must be formated like this: hh:mm or hh:mm:ss.");

        while (true)
        {
            Console.Write("Trigger time: ");
            var time = Console.ReadLine();
            if(string.IsNullOrEmpty(time)) continue;

            var parsedTime = time.Split(':');
            if(parsedTime.Length == 2)
                TriggerTime = new TimeSpan(int.Parse(parsedTime[0]), int.Parse(parsedTime[1]), 0);
            else if(parsedTime.Length == 3)
                TriggerTime = new TimeSpan(int.Parse(parsedTime[0]), int.Parse(parsedTime[1]), int.Parse(parsedTime[2]));
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Time needs to be in this format: hh:mm or hh:mm:ss");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }

            while (true)
            {
                Console.Write("Text        : ");
                var text = Console.ReadLine();
                if(string.IsNullOrEmpty(text)) continue;
                TimeText = text;
                break;
            }

            nextTriggerTime = DateTime.Today + TriggerTime;
            break;
        }
    }

    public async Task<Notification[]> Process()
    {
        var nofitications = new List<Notification>();

        if (DateTime.Now >= nextTriggerTime)
        {
            var notification = new Notification()
            {
                Message = TimeText,
                Sender = this,
                ImageSourceURL = null,
                GoToLink = null,
                Time = nextTriggerTime
            };
            nofitications.Add(notification);
            
            nextTriggerTime = DateTime.Today.AddDays(1) + TriggerTime;
        }

        return nofitications.ToArray();
    }

    public void AppendSaveInfo(BinaryWriter writer)
    {
        writer.Write(TypeName);
        writer.Write(id);
        writer.Write($"{TriggerTime.Hours}:{TriggerTime.Minutes}:{TriggerTime.Seconds}");
        writer.Write(TimeText);
    }

    public static IPingLineNotifier CreateFromSave(string notifID, BinaryReader reader)
    {
        var saved = new PLNTime(notifID, false);

        var parsedTime = reader.ReadString().Split(':');
        if(parsedTime.Length == 2)
            saved.TriggerTime = new TimeSpan(int.Parse(parsedTime[0]), int.Parse(parsedTime[1]), 0);
        else if(parsedTime.Length == 3)
            saved.TriggerTime = new TimeSpan(int.Parse(parsedTime[0]), int.Parse(parsedTime[1]), int.Parse(parsedTime[2]));
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Could not load notifier of type time with ID: {notifID}. Set trigger time to 12:00");
            Console.ForegroundColor = ConsoleColor.White;
            saved.TriggerTime = new TimeSpan(12, 00, 00);
        }

        saved.TimeText = reader.ReadString();
        saved.nextTriggerTime = DateTime.Today + saved.TriggerTime;
        return saved;
    }

    public string GetName() => Name;
    public string GetTypeName() => TypeName;
}