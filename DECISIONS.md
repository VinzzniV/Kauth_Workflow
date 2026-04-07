# DECISIONS.md

## 1. Produktziel
Die Anwendung entwickelt sich zu einer versionierten internen Workflow-Plattform.
Nicht weiter zu einem groesseren spezialisierten Onboarding-Tool.

---

## 2. Backend ist Source of Truth
Alle Business-Regeln muessen im Backend korrekt sein.
Frontend ist Darstellung und Bedienoberflaeche, nicht zweite Regelquelle.

---

## 3. Rollen != Responsibilities
- Rollen = Zugriff
- Responsibilities = fachliche Ownership
Niemals vermischen.

---

## 4. Assignment bleibt strikt
- `user` = persoenlich
- `responsibility` = geteilte fachliche Zustaendigkeit
Keine Leaks oder impliziten Kurzschluesse.

---

## 5. Workflow-Definition ist eigener Kern
Die Plattform braucht einen expliziten Definition Layer.
Task Templates und Prozessarten allein sind nicht das Endmodell.

---

## 6. Versionierung ist Pflicht
Workflow-Definitionen muessen versioniert werden.
Laufende Instanzen duerfen durch spaetere Admin-Aenderungen nicht brechen.

---

## 7. Runtime ist mehr als Task-Generierung
Tasks sind nur eine moegliche Laufzeitwirkung.
Die Engine darf nicht weiter nur als Task-Generator gedacht werden.

---

## 8. Migration statt Big Bang
Neue Architektur wird parallel zur Altwelt eingefuehrt.
Altlogik erst nach Stabilitaet und Paritaet zurueckbauen.

---

## 9. Keine freie technische Magie im Builder
Admins duerfen fachliche Konfiguration pflegen.
Admins duerfen keine freie PowerShell-, SQL- oder HTTP-Automation hinterlegen.

---

## 10. Actions sind kontrollierte Produktelemente
Technische Actions wie `CreateAdUser` oder `SendWelcomeMail` sind freigegebene, validierte Bausteine.
Keine lose Scripting-Funktion.

---

## 11. Completion-Regel bleibt streng
Ein Workflow gilt erst als abgeschlossen, wenn alle relevanten Laufzeitpfade sauber beendet sind.
Keine UI-Abkuerzungen.

---

## 12. Statuskonsistenz bleibt Pflicht
- `cancelled` nicht auf `completed` mappen
- Statusunterschiede nicht verstecken
- `skipped` nicht als Reparatur fuer falsch generierte Tasks missbrauchen

---

## 13. Parallelitaet bleibt erlaubt
Mehrere Teams oder Rollen koennen parallel arbeiten.
Das Modell darf keine unnoetige serielle Einbahnstrasse erzwingen.

---

## 14. Bestehendes Fachwissen bleibt wertvoll
Task Templates, Conditions, Dependencies, Audit und Assignment-Konzepte werden weiterverwendet.
Sie werden in das neue Plattformmodell ueberfuehrt statt verworfen.

---

## 15. Onboarding ist nur ein Workflow
Onboarding bleibt wichtig, aber kein versteckter Produktkern.
Neue Kernlogik darf nicht onboarding-spezifisch verengt werden.

---

## 16. Identity-Grundsatz bleibt bestehen
- Die App ist nicht das fuehrende Benutzersystem
- AD bzw. Entra liefern technische Identitaet
- Person und technische Identity bleiben getrennt

---

## 17. Gruppen sind Standard fuer Zugriff
Standardzugriff kommt ueber Gruppen-Mapping.
Lokale Sonderfaelle bleiben Ausnahme, nicht Primarmodell.

---

## 18. Refactors brauchen einen klaren Grund
Keine grossen unstrukturierten Refactors nebenbei.
Bei Kernumbauten zuerst Zielmodell und Migrationsschnitt klaeren.

---

## 19. Sicherheit vor Bequemlichkeit
Keine freien technischen Seiteneffekte fuer Nicht-Entwickler.
Keine Secrets in Artefakten oder unklaren Runtime-Pfaden.

---

## 20. Dokumentation ist Teil der Architekturarbeit
Zielbild, Migration und Begriffe muessen im Repo nachvollziehbar sein.
Architekturarbeit ohne Doku gilt nicht als fertig.
