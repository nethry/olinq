namespace OLinq.Exceptions;

public class ODataTranslationException : Exception
{
    public ODataTranslationException(string message) : base(message) { }
    public ODataTranslationException(string message, Exception inner) : base(message, inner) { }
}
