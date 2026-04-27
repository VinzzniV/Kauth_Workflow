-- Rename training departments from the older "Azubis ..." labels to the
-- product names used in UI and configuration.

UPDATE departments
SET name = 'Ausbildung technisch'
WHERE LOWER(name) = 'azubis technisch';

UPDATE departments
SET name = 'Ausbildung kaufmaennisch'
WHERE LOWER(name) = 'azubis kaufmaennisch';
