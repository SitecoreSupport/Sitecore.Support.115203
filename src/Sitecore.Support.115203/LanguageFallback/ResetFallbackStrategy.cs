using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Data.Managers;
using Sitecore.Pipelines;

namespace Sitecore.Support.LanguageFallback
{
    public class ResetFallbackStrategy
    {
        public void Process(PipelineArgs args)
        {
            LanguageFallbackManager.Strategy = new Sitecore.Support.Data.Managers.DefaultLanguageFallbackStrategy();
        }
    }
}