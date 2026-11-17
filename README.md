# 📄 ResumeIQ – AI Resume Analyzer

An **ASP.NET Core 8 MVC** application that lets users upload a CV (PDF/DOCX), extracts its text, sends it to an **AI API** and shows a detailed, visual analysis with a score out of 100, job-description matching and saved history.

---

## ✨ Features

| Requirement | Implementation |
|---|---|
| Upload PDF / DOCX resume | Drag & drop upload, extension + size + file-signature validation (max 5 MB) |
| Extract resume text | PDF → **PdfPig**, DOCX → **OpenXML SDK** |
| Send content to an AI API | **Google Gemini** (default) or **OpenAI** / any OpenAI-compatible API |
| Analyze skills, experience, education | Skill chips, experience timeline, education list |
| Missing skills | Recommended skills to add for the target role |
| Resume quality | Rating + strengths & weaknesses |
| Suggested improvements | Numbered, actionable tips |
| Score (e.g. 78/100) | Animated score ring + 5-part breakdown (Skills, Experience, Education, Formatting, Impact) |
| Match against job description | Match % + **matching** (green) and **missing** (red) skills + verdict |
| Store analysis history | SQL Server via EF Core – history page with search, pagination, view & delete |

Extras: loading screen while analysing, Print / Save as PDF report, friendly error messages, **Demo mode** (works offline without an API key).

---

## 🛠 Tech Stack
ASP.NET Core 8 MVC · Entity Framework Core 8 · SQL Server (LocalDB) · PdfPig · DocumentFormat.OpenXml · Bootstrap 5 · Gemini / OpenAI API

---

## 🚀 How to Run

1. Open `ResumeAnalyzer.sln` in **Visual Studio 2022** (.NET 8 SDK).
2. Press **F5**. The database `ResumeAnalyzerDb` is created automatically.
3. Upload a resume, optionally paste a job description, click **Analyze Resume**.

### 🔑 Add an AI API key (recommended)
Without a key the app runs in **Demo mode** (offline keyword-based analysis). For real AI analysis:

**Google Gemini (free):** get a key from <https://aistudio.google.com/apikey> and set in `appsettings.json`:
```json
"AiSettings": {
  "Provider": "Gemini",
  "ApiKey": "YOUR_GEMINI_KEY",
  "Model": "gemini-2.5-flash"
}
```

**OpenAI:**
```json
"AiSettings": {
  "Provider": "OpenAI",
  "ApiKey": "sk-...",
  "Model": "gpt-4o-mini",
  "OpenAiBaseUrl": "https://api.openai.com/v1"
}
```
> Any OpenAI-compatible service (Groq, OpenRouter …) works by changing `OpenAiBaseUrl` and `Model`.

The active provider is shown in the top-right badge of the navbar.

---

## 🔄 How it works
```
Upload (PDF/DOCX) → Validate → Extract text → Build prompt (+ job description)
   → AI API returns structured JSON → Normalize scores → Save to SQL Server → Show report
```

## 🗂 Project Structure
```
ResumeAnalyzer.Web/
├── Controllers/        HomeController (upload/analyze), AnalysisController (details, history, delete)
├── Services/
│   ├── ResumeTextExtractor.cs     PDF & DOCX text extraction
│   ├── ResumeAnalysisService.cs   Workflow + database
│   └── AI/                        Prompt, Gemini, OpenAI and Demo analyzers
├── Models/             Entity + AI result model + settings
├── ViewModels/         Upload (with validation), History, Details
├── Data/               AppDbContext
├── Views/              Home, Analysis (Details, History), Shared layout
└── wwwroot/            site.css (custom design), site.js (drag & drop, animations)
database/ResumeAnalyzerDb.sql
```

---
**Developer:** Syed Muhammad Shakeel · [GitHub](https://github.com/salahuddeen-it)
