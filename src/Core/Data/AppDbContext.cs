// ... (начало файла без изменений)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<ExperimentResult>()
        .HasOne(er => er.Experiment)
        .WithMany()
        .HasForeignKey(er => er.ExperimentId)
        .OnDelete(DeleteBehavior.Cascade);

    // Составной индекс для молниеносного поиска кэша (теперь по явным свойствам)
    modelBuilder.Entity<ExperimentResult>()
        .HasIndex(er => new { er.AlgorithmId, er.N, er.DataType, er.M })
        .HasDatabaseName("IX_ExperimentResult_CacheLookup");

    modelBuilder.Entity<Experiment>()
        .HasIndex(e => e.AlgorithmId)
        .HasDatabaseName("IX_Experiment_AlgorithmId");
}