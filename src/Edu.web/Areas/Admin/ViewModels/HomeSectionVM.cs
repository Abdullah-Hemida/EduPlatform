
namespace Edu.Web.Areas.Admin.ViewModels
{
   public class HomeSectionEditVm
   {
       public int Id { get; set; }
       public int Order { get; set; }
       public string? ExistingSectionImageKey { get; set; }
       public string? ExistingSectionImageUrl { get; set; }

       // translations for the section (one per culture)
       public List<HomeSectionTranslationVm> Translations { get; set; } = new();

       // items
     public List<HomeSectionItemEditVm> Items { get; set; } = new();
   }

        public class HomeSectionTranslationVm
        {
            public string Culture { get; set; } = "en";
            public string? Title { get; set; }
            public string? Subtitle { get; set; }
        }

        public class HomeSectionItemEditVm
        {
            public int Id { get; set; }
            public int Order { get; set; }
            public string? ExistingImageKey { get; set; }
            public string? ExistingImageUrl { get; set; }

            // fallback text (optional)
            public string? Text { get; set; }

            public string? LinkUrl { get; set; }

            // posted files (model binder will populate if input has name Items[i].ImageFile)
            public IFormFile? ImageFile { get; set; }

            // remove checkbox flags (posted)
            public bool RemoveImage { get; set; }

            // translations per item
            public List<HomeSectionItemTranslationVm> Translations { get; set; } = new();
        }

        public class HomeSectionItemTranslationVm
        {
            public string Culture { get; set; } = "en";
            public string? Text { get; set; }
        }
    }

