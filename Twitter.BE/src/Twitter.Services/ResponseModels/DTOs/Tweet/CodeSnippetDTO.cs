﻿using Twitter.Services.ResponseModels.Interfaces;

namespace Twitter.Services.ResponseModels.DTOs.Tweet
{
    public class CodeSnippetDTO : IResponseDTO
    {
        public string Text { get; set; }
        public int ProgrammingLanguageId { get; set; }
        public string ProgrammingLanguageName { get; set; }
        public string ProgrammingLanguageCode{ get; set; }

        public string GistURL { get; set; }
    }
}
