using BCryptNet = BCrypt.Net.BCrypt;

class Program
{
    static void Main(string[] args)
    {
        System.Console.WriteLine(BCryptNet.HashPassword(args[0]));
    }
}
