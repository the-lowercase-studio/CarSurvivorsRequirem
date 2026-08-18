# Dokumentacja Projektu dla Użytkowników (.user-docs)

W tym folderze znajduje się dokumentacja projektu *Car Survivors* skierowana bezpośrednio do **ludzi** — deweloperów, projektantów gier (game designerów), artystów oraz graczy.

## Cel i Charakterystyka

- **Przystępny język**: Tłumaczenie skomplikowanych mechanik i systemów na intuicyjne pojęcia, diagramy oraz analogie.
- **Kontekst użytkowy**: Wyjaśnienie *dlaczego* coś działa w dany sposób i *jak* z tego korzystać (np. konfiguracja w Inspektorze Unity, projektowanie nowych umiejętności, balansowanie parametrów).
- **Wizualizacje**: Diagramy przepływu (Mermaid), tabele parametrów i schematy zależności.

## Zasady dla Agentów AI (Source-of-Truth & Creation Policy)

1. **Izolacja wiedzy**: Agenci AI **nie traktują** plików z tego folderu jako źródła prawdy operacyjnej (Operational Source of Truth). Agent czerpie wiedzę bezpośrednio z kodu źródłowego (`Assets/Scripts/...`) oraz technicznych wytycznych w `.agents/`.
2. **Tworzenie na żądanie**: Pliki w `.user-docs/` **nie powstają automatycznie** podczas zwykłych zadań programistycznych. Powstają lub są aktualizowane **wyłącznie na wyraźną prośbę użytkownika** (np. za pomocą skilla `create-user-doc`).

## Spis Dostępnych Dokumentacji

*Brak dokumentów — nowe pliki pojawią się tutaj po ich wygenerowaniu na żądanie użytkownika.*
