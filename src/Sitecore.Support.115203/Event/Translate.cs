namespace Sitecore.Support.Event
{
    public class Translate
    {
        public void ClearCache(object sender, System.EventArgs args)
        {
            Sitecore.Globalization.Translate.ResetCache(true);
        }
    }
}