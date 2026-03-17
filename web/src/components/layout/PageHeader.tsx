type Props = {
  title: string;
  description: string;
};

export default function PageHeader({ title, description }: Props) {
  return (
    <header className="page-header">
      <h1 className="page-title">{title}</h1>
      <p className="page-description">{description}</p>
    </header>
  );
}
