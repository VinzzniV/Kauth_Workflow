type Props = {
  title: string;
  description?: string;
};

export default function PageHeader({ title, description }: Props) {
  return (
    <header className={`page-header${description ? " page-header--with-description" : " page-header--compact"}`}>
      <h1 className="page-title">{title}</h1>
      {description ? <p className="page-description">{description}</p> : null}
    </header>
  );
}
