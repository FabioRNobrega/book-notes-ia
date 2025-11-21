using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebApp.TagHelpers;

[HtmlTargetElement("sl-input", Attributes = "asp-for", TagStructure = TagStructure.WithoutEndTag)]
public class ShoelaceInputTagHelper(IHtmlGenerator generator) 
    : InputTagHelper(generator);
