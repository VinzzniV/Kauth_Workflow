-- Slice 5 (Admin-Gated-Automation, Referenzuser-Mapping):
-- person_lookup als neuer InputType fuer workflow_answers und workflow_answer_definitions.
-- Antwort wird ueber value_number = person.id gespeichert (keine neue Spalte).
-- Begleitende Whitelist-Erweiterungen: types/workflow.ts RequirementInputType,
-- WorkflowSummaryBuilder.HasAnswer, FE-DTO-Unions, Admin-Authoring (AdminAnswerDefinitionSection).

ALTER TABLE public.workflow_answer_definitions
    DROP CONSTRAINT workflow_answer_definitions_input_type_check;

ALTER TABLE public.workflow_answer_definitions
    ADD CONSTRAINT workflow_answer_definitions_input_type_check
    CHECK (((input_type)::text = ANY ((ARRAY['boolean'::character varying, 'text'::character varying, 'select'::character varying, 'multi_select'::character varying, 'person_lookup'::character varying])::text[])));

ALTER TABLE public.workflow_answers
    DROP CONSTRAINT workflow_answers_input_type_check;

ALTER TABLE public.workflow_answers
    ADD CONSTRAINT workflow_answers_input_type_check
    CHECK (((input_type)::text = ANY ((ARRAY['boolean'::character varying, 'text'::character varying, 'select'::character varying, 'multi_select'::character varying, 'person_lookup'::character varying])::text[])));
